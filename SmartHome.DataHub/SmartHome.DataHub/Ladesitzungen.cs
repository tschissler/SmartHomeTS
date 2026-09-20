using SharedContracts;

namespace SmartHome.DataHub
{
    /// <summary>What came of trying to turn a session into a row of the table.</summary>
    public enum Zusammenfuehrung
    {
        /// <summary>Both sides agreed and the row is ready to be written.</summary>
        Geschrieben,

        /// <summary>The vehicle is still plugged in. The record is made when the session ends.</summary>
        SitzungLaeuftNoch,

        /// <summary>The box has no assignment at all — the RulesEngine has never spoken for it.</summary>
        ZuordnungFehlt,

        /// <summary>
        /// Both sides exist but talk about different sessions. Nothing is written; see
        /// <see cref="Ladesitzungen"/>.
        /// </summary>
        SitzungsIdPasstNicht,

        /// <summary>This session has already been written in this process.</summary>
        BereitsGeschrieben,

        /// <summary>No usable begin or end, so the row would have no place on the time axis.</summary>
        ZeitenUnbrauchbar,
    }

    /// <summary>
    /// One row of the <c>ladesitzungen</c> table, ready to write. The column names of
    /// Docs/Ladeprotokoll.md are produced where it is written, not here — this is the content.
    /// </summary>
    public sealed record Ladesitzungssatz
    {
        /// <summary>The only tag of the table: <c>Garage</c> or <c>Stellplatz</c>.</summary>
        public required string Wallbox { get; init; }

        /// <summary>The <c>time</c> of the row: when the vehicle was plugged in.</summary>
        public required DateTimeOffset Beginn { get; init; }

        /// <summary>
        /// The wire name of how <see cref="Beginn"/> was arrived at, e.g. <c>steckflanke</c>.
        /// Written rather than derived away, because <see cref="Beginn"/> is the time axis of
        /// the row and <see cref="DauerSekunden"/> is computed from it: a reader has to be able
        /// to tell a measured duration from an estimated one, and only this column says so.
        /// </summary>
        public required string Beginnquelle { get; init; }

        public required int SitzungsId { get; init; }

        /// <summary>The vehicle, or "-" when none could be named.</summary>
        public required string Fahrzeug { get; init; }

        /// <summary>The wire name of the confidence, e.g. <c>erkannt</c>.</summary>
        public required string Vertrauen { get; init; }

        public required DateTimeOffset Ende { get; init; }

        /// <summary>How long the vehicle was plugged in.</summary>
        public required long DauerSekunden { get; init; }

        /// <summary>How long it actually charged — a different number, often much smaller.</summary>
        public required long LadezeitSekunden { get; init; }

        /// <summary>The energy of the session as the box counted it.</summary>
        public required decimal EnergieKwh { get; init; }

        public required decimal EnergiePvKwh { get; init; }
        public required decimal EnergieBatterieKwh { get; init; }
        public required decimal EnergieNetzKwh { get; init; }

        /// <summary>
        /// Box energy minus the three attributed shares. <b>May be slightly negative and is not
        /// clamped</b> — rounding and the one cycle by which the meters trail the box produce
        /// that, and a small negative number is more honest than a silent correction.
        /// </summary>
        public required decimal EnergieUnzugeordnetKwh { get; init; }
    }

    /// <summary>The outcome of one attempt, with a sentence a log line can carry.</summary>
    public readonly record struct Zusammenfuehrungsergebnis(
        Zusammenfuehrung Art, Ladesitzungssatz? Satz, string Begruendung);

    /// <summary>
    /// Joins the ChargingController's <see cref="Ladesitzung"/> with the RulesEngine's
    /// <see cref="FahrzeugZuordnung"/> and produces the rows of the <c>ladesitzungen</c> table.
    /// A pure decision — no broker, no clock, no database — so every rule below is testable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only a matching session id is written.</b> The record has three contributors and they
    /// publish on three topics; the id is the key that holds them together, and checking it is
    /// the whole point of having one. Two topics that disagree are not averaged, not guessed at
    /// and not half written — nothing is written and the case is logged.
    /// </para>
    /// <para>
    /// <b>Both topics are retained</b>, so the broker replays them on every subscribe and the
    /// two can arrive in either order. Every message is therefore an occasion to try again,
    /// from whichever side it came.
    /// </para>
    /// <para>
    /// <b>Writing twice is harmless, writing again for nothing is not.</b> The row is keyed by
    /// table, tag set and time, so a repeated write of the same session replaces the same row —
    /// which is exactly why <c>fahrzeug</c> and <c>vertrauen</c> are fields and not tags. The
    /// guard on the last written session id on top of that keeps a retained replay from
    /// enqueueing the same point over and over, the same job
    /// <c>LastChargingSessionPublishedViaMQTT</c> does in the KebaConnector.
    /// </para>
    /// </remarks>
    public sealed class Ladesitzungen
    {
        /// <summary>When no vehicle could be named. Same spelling as everywhere else in this database.</summary>
        public const string OhneFahrzeug = "-";

        /// <summary>
        /// Decimals the kWh columns are rounded to. A microwatt hour — far below anything the
        /// box or the Envoy can resolve, and enough to keep the unattributed remainder from
        /// carrying the binary noise of the conversion to Float64.
        /// </summary>
        private const int Nachkommastellen = 6;

        private sealed class Stand
        {
            public Ladesitzung? Sitzung;
            public FahrzeugZuordnung? Zuordnung;
            public int? ZuletztGeschrieben;
        }

        private readonly Dictionary<string, Stand> staende = new();

        /// <summary>Takes a session record and tries to make a row of it.</summary>
        public Zusammenfuehrungsergebnis MeldeSitzung(string wallbox, Ladesitzung sitzung)
        {
            var stand = Hole(wallbox);
            stand.Sitzung = sitzung;
            return Versuche(wallbox, stand);
        }

        /// <summary>
        /// Takes an assignment and tries again — the session may have been waiting for it.
        /// </summary>
        public Zusammenfuehrungsergebnis MeldeZuordnung(string wallbox, FahrzeugZuordnung zuordnung)
        {
            var stand = Hole(wallbox);
            stand.Zuordnung = zuordnung;
            return Versuche(wallbox, stand);
        }

        private Zusammenfuehrungsergebnis Versuche(string wallbox, Stand stand)
        {
            if (stand.Sitzung is not Ladesitzung sitzung)
            {
                return new(Zusammenfuehrung.SitzungLaeuftNoch, null,
                    $"{wallbox}: no session record yet.");
            }

            if (sitzung.Zustand != Ladesitzungszustand.Beendet)
            {
                return new(Zusammenfuehrung.SitzungLaeuftNoch, null,
                    $"{wallbox}: session {sitzung.SitzungsId} is still running.");
            }

            // Checked before the assignment, so that a replay of an already written session does
            // not complain about a missing assignment once per restart.
            if (stand.ZuletztGeschrieben == sitzung.SitzungsId)
            {
                return new(Zusammenfuehrung.BereitsGeschrieben, null,
                    $"{wallbox}: session {sitzung.SitzungsId} was already written.");
            }

            if (sitzung.Beginn is not DateTimeOffset beginn || sitzung.Ende is not DateTimeOffset ende)
            {
                return new(Zusammenfuehrung.ZeitenUnbrauchbar, null,
                    $"{wallbox}: session {sitzung.SitzungsId} has no usable begin or end "
                  + $"(begin {Zeit(sitzung.Beginn)}, end {Zeit(sitzung.Ende)}) — the row would have "
                  + "no place on the time axis, so nothing is written.");
            }

            if (ende < beginn)
            {
                return new(Zusammenfuehrung.ZeitenUnbrauchbar, null,
                    $"{wallbox}: session {sitzung.SitzungsId} ends before it begins "
                  + $"({ende:o} < {beginn:o}) — nothing written.");
            }

            if (stand.Zuordnung is not FahrzeugZuordnung zuordnung)
            {
                return new(Zusammenfuehrung.ZuordnungFehlt, null,
                    $"{wallbox}: session {sitzung.SitzungsId} ended, but no assignment has ever "
                  + "arrived for this box. Nothing written — the row is made from both topics or "
                  + "from neither.");
            }

            if (zuordnung.SitzungsId != sitzung.SitzungsId)
            {
                return new(Zusammenfuehrung.SitzungsIdPasstNicht, null,
                    $"{wallbox}: the controller reports session {sitzung.SitzungsId}, the assignment "
                  + $"reports {Nummer(zuordnung.SitzungsId)}. Nothing written — a record of two topics "
                  + "that disagree would be a guess.");
            }

            var pv = Runde(sitzung.EnergiePvKwh);
            var batterie = Runde(sitzung.EnergieBatterieKwh);
            var netz = Runde(sitzung.EnergieNetzKwh);
            var box = Runde(sitzung.EnergieBoxKwh);

            var satz = new Ladesitzungssatz
            {
                Wallbox = wallbox,
                Beginn = beginn,
                Beginnquelle = Beginnquellen.Drahtname(sitzung.Beginnquelle),
                SitzungsId = sitzung.SitzungsId,
                Fahrzeug = string.IsNullOrWhiteSpace(zuordnung.Fahrzeug) ? OhneFahrzeug : zuordnung.Fahrzeug,
                Vertrauen = Vertrauensgrade.Drahtname(zuordnung.Vertrauen),
                Ende = ende,
                DauerSekunden = (long)Math.Round((ende - beginn).TotalSeconds),
                LadezeitSekunden = sitzung.LadezeitSekunden,
                EnergieKwh = box,
                EnergiePvKwh = pv,
                EnergieBatterieKwh = batterie,
                EnergieNetzKwh = netz,
                // Not clamped on purpose: see Ladesitzungssatz.EnergieUnzugeordnetKwh.
                EnergieUnzugeordnetKwh = Runde(box - (pv + batterie + netz)),
            };

            stand.ZuletztGeschrieben = sitzung.SitzungsId;
            return new(Zusammenfuehrung.Geschrieben, satz,
                $"{wallbox}: session {satz.SitzungsId} written — {satz.Fahrzeug} ({satz.Vertrauen}), "
              + $"{satz.EnergieKwh:F3} kWh in {satz.LadezeitSekunden} s of charging over "
              + $"{satz.DauerSekunden} s plugged in (from {satz.Beginnquelle}), of which PV "
              + $"{satz.EnergiePvKwh:F3}, battery "
              + $"{satz.EnergieBatterieKwh:F3}, grid {satz.EnergieNetzKwh:F3}, unattributed "
              + $"{satz.EnergieUnzugeordnetKwh:F3} kWh.");
        }

        private static decimal Runde(decimal wert) => Math.Round(wert, Nachkommastellen);

        private static string Zeit(DateTimeOffset? wert) => wert?.ToString("o") ?? "—";

        private static string Nummer(int? wert) => wert?.ToString() ?? "none";

        private Stand Hole(string wallbox)
        {
            if (!staende.TryGetValue(wallbox, out var stand))
            {
                stand = new Stand();
                staende[wallbox] = stand;
            }
            return stand;
        }
    }
}
