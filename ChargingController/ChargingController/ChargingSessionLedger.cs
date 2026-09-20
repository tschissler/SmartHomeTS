using SharedContracts;

namespace ChargingController
{
    /// <summary>
    /// Follows the charging session of every wallbox: when it began, when it ended, and how much
    /// of the three virtual meters belongs to it. The meters themselves are kept by
    /// <see cref="EnergyAttributionLedger"/>; this class only ever forms differences between two
    /// of their readings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controller is the only author of the session topic because it is the only service
    /// that watches the boundaries in the 5 s cycle. The box numbers the session but cannot date
    /// it — its clock reports "timeQ": 0 — and it knows nothing about where the energy came from.
    /// </para>
    /// <para>
    /// Like the meters, a running session must survive a rollout: the ledger refuses to count for
    /// a box until its own retained payload has arrived or the grace period has passed. Without
    /// that rule a restart in the middle of a charge would publish a fresh session over the
    /// retained one and cut the charge in two.
    /// </para>
    /// </remarks>
    public sealed class ChargingSessionLedger
    {
        /// <summary>
        /// How long the ledger waits for its own retained topic before it accepts that there is
        /// nothing to restore. Same value and same reason as
        /// <see cref="EnergyAttributionLedger.Wiederherstellungsfrist"/>.
        /// </summary>
        public static readonly TimeSpan Wiederherstellungsfrist = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Oldest session start the ledger will still take from the clock of the box. That clock
        /// is not synchronised and may be arbitrarily wrong; a value older than this is not a
        /// long charge but a wrong clock, and writing it would put the session record years into
        /// the past where nobody looks for it.
        /// </summary>
        public static readonly TimeSpan MaximaleSteckdauer = TimeSpan.FromDays(14);

        /// <summary>The difference of the four cumulative counters between two cycles.</summary>
        private readonly record struct Zaehlerdifferenz(
            decimal Pv, decimal Batterie, decimal Netz, long LadezeitSekunden);

        private sealed class Buch
        {
            public Ladesitzung? Sitzung;
            public Ladesitzung? ZuletztVeroeffentlicht;
            public Energieaufteilung? LetzterZaehlerstand;
            public bool Wiederhergestellt;
            public bool StartGemeldet;
            /// <summary>Whether this ledger has already processed a status of this box. False
            /// means a session it sees now may have been running long before the process was.</summary>
            public bool StatusGesehen;
        }

        private readonly Dictionary<string, Buch> buecher = new();
        private readonly DateTimeOffset gestartet;
        private readonly TimeSpan frist;

        public ChargingSessionLedger(DateTimeOffset gestartet, TimeSpan? wiederherstellungsfrist = null)
        {
            this.gestartet = gestartet;
            frist = wiederherstellungsfrist ?? Wiederherstellungsfrist;
        }

        /// <summary>
        /// Takes a session that arrived on the box's own retained topic. Only the first one
        /// counts, and only while the ledger is still waiting for it.
        /// </summary>
        /// <remarks>
        /// Once the ledger has given up waiting it has started keeping the session itself, and
        /// every message that arrives after that is the echo of its own publication. Adopting
        /// the echo would change nothing about the content and would log a "restored" line in
        /// the middle of a run that restored nothing.
        /// </remarks>
        public void Wiederherstellen(string wallbox, Ladesitzung sitzung)
        {
            var b = Hole(wallbox);
            if (b.Wiederhergestellt || b.StartGemeldet)
                return;

            b.Sitzung = sitzung;
            b.ZuletztVeroeffentlicht = sitzung;
            b.Wiederhergestellt = true;
            b.StartGemeldet = true;
            Console.WriteLine($"Ladesitzung {wallbox}: restored session {sitzung.SitzungsId} " +
                $"({sitzung.Zustand}, started {sitzung.Beginn:u}, PV {sitzung.EnergiePvKwh:F3} kWh, " +
                $"battery {sitzung.EnergieBatterieKwh:F3} kWh, grid {sitzung.EnergieNetzKwh:F3} kWh, " +
                $"box {sitzung.EnergieBoxKwh:F3} kWh, charging time {sitzung.LadezeitSekunden} s, " +
                $"last written {sitzung.Zeitpunkt:u})");
        }

        /// <summary>Whether the ledger may act on this box yet — see <see cref="Wiederherstellungsfrist"/>.</summary>
        public bool Bereit(string wallbox, DateTimeOffset jetzt)
        {
            var b = Hole(wallbox);
            if (b.Wiederhergestellt)
                return true;
            if (jetzt - gestartet < frist)
                return false;

            if (!b.StartGemeldet)
            {
                b.StartGemeldet = true;
                Console.WriteLine($"Ladesitzung {wallbox}: no retained session arrived within " +
                    $"{frist.TotalSeconds:F0} s. Expected on first commissioning; at any other time a " +
                    "session that was running is now dated from here rather than from when it began.");
            }
            return true;
        }

        /// <summary>
        /// Carries the session bookkeeping of one box forward by one cycle and returns what has
        /// to be published, in order — usually nothing or one payload, two when a session ends
        /// and the next one starts within the same cycle.
        /// </summary>
        /// <param name="status">The last status this box published.</param>
        /// <param name="zaehlerstand">
        /// The meter reading <see cref="EnergyAttributionLedger.Fortschreiben"/> just returned
        /// for this box. Hand in the reading of this cycle, not the one of the previous: the
        /// session energy is the difference between the reading at its start and the one at its
        /// end, and both have to come from the same source.
        /// </param>
        /// <remarks>
        /// The interval that just passed is booked onto the session that was running <i>during</i>
        /// it, which at a session boundary is the one that is ending. A session that opens in this
        /// cycle starts at zero and gets nothing of it — the time before the plug went in is not
        /// its energy.
        /// </remarks>
        public IReadOnlyList<Ladesitzung> Fortschreiben(
            string wallbox,
            WallboxStatus status,
            Energieaufteilung zaehlerstand,
            DateTimeOffset jetzt)
        {
            if (!Bereit(wallbox, jetzt))
                return [];

            var b = Hole(wallbox);
            var delta = b.LetzterZaehlerstand is null
                ? null
                : Differenz(wallbox, b.LetzterZaehlerstand, zaehlerstand);
            b.LetzterZaehlerstand = zaehlerstand;

            var veroeffentlichen = new List<Ladesitzung>();
            var laufend = b.Sitzung is { Zustand: Ladesitzungszustand.Laufend } ? b.Sitzung : null;

            if (laufend is not null && status.SitzungsId != laufend.SitzungsId)
            {
                // Either the plug was pulled or the next session has already opened. Either way
                // this one is over, and the interval that just passed is still its own.
                var ende = b.StatusGesehen ? jetzt : laufend.Zeitpunkt;
                var beendet = Anwenden(laufend, delta) with
                {
                    Zustand = Ladesitzungszustand.Beendet,
                    Ende = ende,
                };
                if (!b.StatusGesehen)
                {
                    Console.WriteLine($"Ladesitzung {wallbox}: session {laufend.SitzungsId} was already over " +
                        $"when this process came back. Its end is dated to {ende:u}, the last moment the " +
                        "controller knew it was running — everything after that was not observed by anybody.");
                }
                else
                {
                    Console.WriteLine($"Ladesitzung {wallbox}: session {laufend.SitzungsId} ended at {ende:u} " +
                        $"— {beendet.EnergieBoxKwh:F3} kWh at the box, of which PV {beendet.EnergiePvKwh:F3}, " +
                        $"battery {beendet.EnergieBatterieKwh:F3}, grid {beendet.EnergieNetzKwh:F3} kWh " +
                        $"over {beendet.LadezeitSekunden} s of charging.");
                }
                Sammle(b, veroeffentlichen, beendet, jetzt);
                laufend = null;
                delta = null; // spent on the session that just ended
            }

            if (laufend is null && status.SitzungsId is int neu)
            {
                var eroeffnet = Eroeffnen(wallbox, neu, status, jetzt, geerbt: !b.StatusGesehen);
                Sammle(b, veroeffentlichen, eroeffnet, jetzt);
            }
            else if (laufend is not null)
            {
                var fortgeschrieben = Anwenden(laufend, delta) with
                {
                    // The box counts this session itself and resets the value when the next one
                    // starts, so it is taken while the session is running and never afterwards.
                    EnergieBoxKwh = status.EnergieSitzungWh / 1000m,
                };
                Sammle(b, veroeffentlichen, fortgeschrieben, jetzt);
            }

            b.StatusGesehen = true;
            return veroeffentlichen;
        }

        /// <summary>The session record of one box as it stands, for diagnostics and tests.</summary>
        public Ladesitzung? Stand(string wallbox) => Hole(wallbox).Sitzung;

        /// <summary>
        /// Opens a session and decides, once, what its start time is.
        /// </summary>
        /// <param name="geerbt">
        /// Whether this is the first status of this box the ledger processes, in which case the
        /// session may have been running long before this process was.
        /// </param>
        /// <remarks>
        /// <b>Read <see cref="WallboxStatus.SitzungsBeginnAusBoxZeit"/> the right way round:</b>
        /// <c>true</c> means the value came from the clock of the box, and that clock reports
        /// "timeQ": 0 — it is the untrustworthy source, not the trustworthy one. The order below
        /// follows from that, and each branch records which of the four
        /// <see cref="Beginnquelle"/> values it used:
        /// <list type="number">
        /// <item><see cref="Beginnquelle.Steckflanke"/> — the plug-in edge the KebaConnector
        /// observed itself. Measured, so it wins;</item>
        /// <item><see cref="Beginnquelle.Boxuhr"/> — for an inherited session, the box clock if
        /// it is at all plausible. Wrong by an unknown amount, but it is the only source that
        /// knows the session started before this process did;</item>
        /// <item><see cref="Beginnquelle.Regelzyklus"/> — this moment, when the ledger watched
        /// the session appear. At most one cycle late;</item>
        /// <item><see cref="Beginnquelle.Dienstanlauf"/> — this moment for an inherited session
        /// with no usable box clock. Too late by an unknown amount, and that is the one thing
        /// the record can still say about it.</item>
        /// </list>
        /// </remarks>
        private static Ladesitzung Eroeffnen(
            string wallbox, int id, WallboxStatus status, DateTimeOffset jetzt, bool geerbt)
        {
            DateTimeOffset beginn;
            Beginnquelle quelle;

            if (status.SitzungsBeginn is DateTimeOffset beobachtet && !status.SitzungsBeginnAusBoxZeit)
            {
                beginn = beobachtet;
                quelle = Beginnquelle.Steckflanke;
            }
            else if (geerbt && status.SitzungsBeginn is DateTimeOffset boxzeit && Plausibel(boxzeit, jetzt))
            {
                beginn = boxzeit;
                quelle = Beginnquelle.Boxuhr;
            }
            else
            {
                beginn = jetzt;
                quelle = geerbt ? Beginnquelle.Dienstanlauf : Beginnquelle.Regelzyklus;
            }

            Console.WriteLine($"Ladesitzung {wallbox}: session {id} started at {beginn:u} " +
                $"(from {Beginnquellen.Drahtname(quelle)})");

            return new Ladesitzung
            {
                SitzungsId = id,
                Beginn = beginn,
                Beginnquelle = quelle,
                Ende = null,
                Zustand = Ladesitzungszustand.Laufend,
                EnergieBoxKwh = status.EnergieSitzungWh / 1000m,
            };
        }

        /// <summary>
        /// Whether a start time taken from the clock of the box can be believed at all: not in
        /// the future, and not longer ago than a vehicle could plausibly have hung on the box.
        /// </summary>
        private static bool Plausibel(DateTimeOffset boxzeit, DateTimeOffset jetzt)
            => boxzeit <= jetzt && jetzt - boxzeit <= MaximaleSteckdauer;

        /// <summary>
        /// Publishes a payload if it says something new. Nothing new, nothing published — and
        /// <see cref="Ladesitzung.Zeitpunkt"/> keeps standing at the last moment something
        /// actually changed, which is what makes it usable as "last known state as of".
        /// </summary>
        private static void Sammle(Buch b, List<Ladesitzung> liste, Ladesitzung kandidat, DateTimeOffset jetzt)
        {
            if (kandidat.GleicheAussageWie(b.ZuletztVeroeffentlicht))
            {
                b.Sitzung = b.ZuletztVeroeffentlicht;
                return;
            }

            var neu = kandidat with { Zeitpunkt = jetzt };
            b.Sitzung = neu;
            b.ZuletztVeroeffentlicht = neu;
            liste.Add(neu);
        }

        private static Ladesitzung Anwenden(Ladesitzung sitzung, Zaehlerdifferenz? delta)
            => delta is not Zaehlerdifferenz d
                ? sitzung
                : sitzung with
                {
                    EnergiePvKwh = sitzung.EnergiePvKwh + d.Pv,
                    EnergieBatterieKwh = sitzung.EnergieBatterieKwh + d.Batterie,
                    EnergieNetzKwh = sitzung.EnergieNetzKwh + d.Netz,
                    LadezeitSekunden = sitzung.LadezeitSekunden + d.LadezeitSekunden,
                };

        /// <summary>
        /// What the meters moved by between two cycles, or null when they moved backwards.
        /// </summary>
        /// <remarks>
        /// The four counters only ever grow. A drop is not negative energy but a meter that was
        /// restored or reset behind our back, and booking it onto a session would take energy off
        /// a charge that really happened. The interval is skipped and shows up as unattributed.
        /// </remarks>
        private static Zaehlerdifferenz? Differenz(string wallbox, Energieaufteilung vorher, Energieaufteilung nachher)
        {
            var pv = nachher.EnergieLadungPvKwh - vorher.EnergieLadungPvKwh;
            var batterie = nachher.EnergieLadungBatterieKwh - vorher.EnergieLadungBatterieKwh;
            var netz = nachher.EnergieLadungNetzKwh - vorher.EnergieLadungNetzKwh;
            var ladezeit = nachher.LadezeitSekunden - vorher.LadezeitSekunden;

            if (pv < 0 || batterie < 0 || netz < 0 || ladezeit < 0)
            {
                Console.WriteLine($"Ladesitzung {wallbox}: the meters went backwards " +
                    $"(PV {vorher.EnergieLadungPvKwh:F3} -> {nachher.EnergieLadungPvKwh:F3} kWh, " +
                    $"battery {vorher.EnergieLadungBatterieKwh:F3} -> {nachher.EnergieLadungBatterieKwh:F3} kWh, " +
                    $"grid {vorher.EnergieLadungNetzKwh:F3} -> {nachher.EnergieLadungNetzKwh:F3} kWh, " +
                    $"charging time {vorher.LadezeitSekunden} -> {nachher.LadezeitSekunden} s). " +
                    "This interval is not booked onto the session.");
                return null;
            }

            return new Zaehlerdifferenz(pv, batterie, netz, ladezeit);
        }

        private Buch Hole(string wallbox)
        {
            if (!buecher.TryGetValue(wallbox, out var b))
            {
                b = new Buch();
                buecher[wallbox] = b;
            }
            return b;
        }
    }
}
