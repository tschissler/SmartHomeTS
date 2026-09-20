using SharedContracts;

namespace RulesEngine.Rules;

/// <summary>
/// Decides which vehicle is charging at which wallbox. Pure function: the running assignments
/// go in and come out again, nothing is held here. Concept and reasoning in
/// Docs/Fahrzeug-Wallbox-Zuordnung.md.
/// </summary>
/// <remarks>
/// <para>
/// The one sentence everything rests on: <b>the wallbox is the truth, and the silence of a
/// vehicle proves nothing.</b> A car that reports nothing is not a car that is somewhere else —
/// it may simply have nothing to say. Every level of evidence below either measures the boxes
/// or reads a positive vehicle report; none of them concludes anything from an absent or
/// negative one. The gemessener Gegenfall of 2026-09-20 is in the concept document: exclusion
/// would have named the BMW with conviction, and the VW was charging.
/// </para>
/// <para>
/// Evidence, highest first:
/// <list type="number">
/// <item>Manual override whose session matches the running one — <c>bestaetigt</c>.</item>
/// <item>A vehicle reports a connected charger while exactly one occupied box is still
/// unassigned — <c>erkannt</c>.</item>
/// <item>The same, with a second box occupied but already firmly assigned. That is an exclusion
/// over the <i>measured</i> occupancy of the boxes, not over vehicle silence, which is why it is
/// allowed. Falls out of the same condition as level 2 and needs no case of its own.</item>
/// <item>The vehicle that last charged at this box — <c>vermutet</c>.</item>
/// <item>Nothing of the above — <c>unbekannt</c>.</item>
/// </list>
/// </para>
/// <para>
/// An assignment sticks until the session ends and is only ever raised. Its errors are silent —
/// a wrong assignment looks exactly like a right one — so where the evidence is ambiguous the
/// rule says <c>vermutet</c> or <c>unbekannt</c> rather than guessing confidently.
/// </para>
/// <para>
/// <b>Every age is taken from the timestamp inside the payload, never from when the message
/// arrived.</b> The whole restart path of this rule runs on retained messages: on startup the
/// assignments, box states, overrides and vehicle data all arrive in one burst and none of them
/// is a fresh event. Judging them by arrival time would make a five week old vehicle report look
/// current. (The MaxStatusAge check of <see cref="MixerPositionRule"/> has exactly that defect;
/// it is a backlog item of its own and deliberately not fixed here.)
/// </para>
/// </remarks>
public sealed class VehicleAssignmentRule
{
    /// <summary>
    /// How old a box state may be and still be acted on. The box reports every few seconds, so
    /// anything beyond this means the connector is gone. The rule then leaves that box's
    /// assignment untouched rather than declaring the session over — a missing connector is not
    /// evidence that the plug was pulled.
    /// </summary>
    public static readonly TimeSpan MaxWallboxAlter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How old a positive vehicle report may be where the session start is not trustworthy.
    /// Generous, because the sources are slow: the VW polls every 15 minutes and its data is
    /// another hour behind on top. Short enough that the BMW's legitimately 35 day old report
    /// of 2026-09-20 cannot be mistaken for a statement about the session running now.
    /// </summary>
    public static readonly TimeSpan MaxFahrzeugAlter = TimeSpan.FromHours(6);

    /// <summary>
    /// How much earlier than the session start a vehicle report may have been measured and still
    /// count for that session. Covers the clock skew between the connectors and this service,
    /// nothing more.
    /// </summary>
    public static readonly TimeSpan SitzungsbeginnToleranz = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long the caller must wait after startup before publishing anything, so the retained
    /// assignments, overrides and box states have all arrived. Publishing earlier would write a
    /// fresh <c>unbekannt</c> over the very topic the running assignment has to be restored
    /// from — and it would do so during exactly the moment when the broker was about to deliver
    /// it. Retained messages arrive within milliseconds of subscribing, so this is generous on
    /// purpose: the cost of waiting is a few seconds of a stale label, the cost of not waiting
    /// is a lost assignment and a history that starts over.
    /// </summary>
    public static readonly TimeSpan Wiederherstellungsfrist = TimeSpan.FromSeconds(15);

    private readonly TimeSpan maxWallboxAlter;
    private readonly TimeSpan maxFahrzeugAlter;
    private readonly TimeSpan sitzungsbeginnToleranz;

    public VehicleAssignmentRule(
        TimeSpan? maxWallboxAlter = null,
        TimeSpan? maxFahrzeugAlter = null,
        TimeSpan? sitzungsbeginnToleranz = null)
    {
        this.maxWallboxAlter = maxWallboxAlter ?? MaxWallboxAlter;
        this.maxFahrzeugAlter = maxFahrzeugAlter ?? MaxFahrzeugAlter;
        this.sitzungsbeginnToleranz = sitzungsbeginnToleranz ?? SitzungsbeginnToleranz;
    }

    /// <summary>
    /// Works out the assignment of every box that currently has a session.
    /// </summary>
    /// <param name="wallboxen">Latest state per box, keyed by the device level of its topic.</param>
    /// <param name="fahrzeuge">Latest state per vehicle, keyed by the device level of its topic.</param>
    /// <param name="korrekturen">Manual overrides per box, as published by the web interface.</param>
    /// <param name="letzteAussage">
    /// The rule's own last statement per box, as it stands on the retained topic. Doubles as the
    /// state of the running session and as the history of the previous one: which of the two it
    /// is, is decided by its SitzungsId.
    /// </param>
    /// <param name="jetzt">Now, in UTC.</param>
    /// <returns>
    /// The target statement for every box with a running, currently reported session. A box
    /// without a session, or whose state is stale, is <b>absent</b> from the result — its
    /// retained statement has to stay standing, because it is the history for the next session.
    /// </returns>
    /// <remarks>
    /// <paramref name="wallboxen"/> is taken as the complete picture of the installation. A box
    /// that has never published at all is invisible here and its occupancy cannot be accounted
    /// for; in practice both boxes come out of the same connector and arrive together as
    /// retained messages before the first evaluation.
    /// </remarks>
    public IReadOnlyDictionary<string, FahrzeugZuordnung> Evaluate(
        IReadOnlyDictionary<string, WallboxStatus> wallboxen,
        IReadOnlyDictionary<string, CarStatusData> fahrzeuge,
        IReadOnlyDictionary<string, FahrzeugZuordnung> korrekturen,
        IReadOnlyDictionary<string, FahrzeugZuordnung> letzteAussage,
        DateTimeOffset jetzt)
    {
        // Ordinal order so the outcome does not depend on the order messages happened to arrive
        // in - the inference below looks across the boxes, and a dictionary has no order.
        var lagen = wallboxen
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(e => Lage(e.Key, e.Value, letzteAussage, jetzt))
            .OfType<BoxLage>()
            .ToList();

        foreach (var lage in lagen)
        {
            // Level 1. A person outranks everything, including an earlier person: only a person
            // ever produces "bestaetigt", so replacing one can only be a second correction.
            if (Korrektur(korrekturen, lage) is string korrigiert)
            {
                var bestaetigt = Aussage(lage, korrigiert, Vertrauensgrad.Bestaetigt, jetzt);
                // An override stands retained for the whole session, so this branch is taken on
                // every evaluation. Keeping the statement that is already there where it says
                // the same thing is what stops the Zeitpunkt from creeping forward a few times a
                // minute — it is meant to say when the assignment was established.
                lage.Ergebnis = bestaetigt.GleicheAussageWie(lage.Aktuell) ? lage.Aktuell : bestaetigt;
                continue;
            }

            // Sticky: whatever was decided for this session earlier stands until something
            // outranks it. This is also the whole of the restart path - the retained statement
            // comes back in and is simply carried on.
            lage.Ergebnis = lage.Aktuell;
        }

        // Placing a vehicle by the occupancy of the boxes is only sound while every box is
        // actually reporting. A box whose state has gone stale has unknown occupancy, and
        // concluding "then it must be the other one" from that would be the exclusion the
        // concept rejects - only over a silent box instead of a silent car.
        if (wallboxen.Values.All(s => jetzt - s.Zeitpunkt <= maxWallboxAlter))
        {
            ErkenneUeberMeldung(lagen, fahrzeuge, jetzt);
        }

        foreach (var lage in lagen.Where(l => l.Ergebnis is null))
        {
            // Level 4 and 5. The history is the vehicle named on this box's own retained topic,
            // left there by the previous session.
            lage.Ergebnis = lage.HistorieFahrzeug is string historie
                ? Aussage(lage, historie, Vertrauensgrad.Vermutet, jetzt)
                : Aussage(lage, null, Vertrauensgrad.Unbekannt, jetzt);
        }

        return lagen.ToDictionary(l => l.Name, l => l.Ergebnis!);
    }

    /// <summary>
    /// Levels 2 and 3: a vehicle that says its charger is connected, placed by the measured
    /// occupancy of the boxes.
    /// </summary>
    /// <remarks>
    /// Both levels are the same condition — <b>exactly one occupied box is still unassigned and
    /// exactly one credible reporting vehicle is still unplaced</b>. With one box occupied that
    /// is level 2; with two occupied and the other one firmly assigned, the only box left for
    /// the reporting vehicle is this one, which is level 3. Where either count is not one the
    /// rule stops: two unassigned occupied boxes cannot be told apart, and two reporting
    /// vehicles cannot either. It then falls through to the history guess, which is visibly a
    /// guess — the failure mode the concept asks for.
    /// </remarks>
    private void ErkenneUeberMeldung(
        List<BoxLage> lagen,
        IReadOnlyDictionary<string, CarStatusData> fahrzeuge,
        DateTimeOffset jetzt)
    {
        var offene = lagen.Where(l => Grad(l.Ergebnis) < Vertrauensgrad.Erkannt).ToList();
        if (offene.Count != 1)
        {
            return;
        }

        var ziel = offene[0];
        var belegt = lagen
            .Where(l => l != ziel && l.Ergebnis?.Fahrzeug is not null)
            .Select(l => l.Ergebnis!.Fahrzeug!)
            .ToHashSet(StringComparer.Ordinal);

        var melder = fahrzeuge
            .Where(e => !belegt.Contains(e.Key) && MeldetVerbunden(e.Value, ziel, jetzt))
            .Select(e => e.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (melder.Count == 1)
        {
            ziel.Ergebnis = Aussage(ziel, melder[0], Vertrauensgrad.Erkannt, jetzt);
        }
    }

    /// <summary>
    /// Whether this vehicle's own report can be about the session running at that box: it says
    /// its charger is connected, and it measured that recently enough to be talking about now.
    /// </summary>
    /// <remarks>
    /// The freshness test is what keeps the level-2 evidence honest. A vehicle reports on its own
    /// schedule and its payload stays retained in between, so "chargerConnected" on its own says
    /// nothing about when. Where the box start time is trustworthy the test is exact — a report
    /// measured before the plug went in is about a different charge — and where it is not, a
    /// plain age window stands in.
    /// </remarks>
    private bool MeldetVerbunden(CarStatusData fahrzeug, BoxLage ziel, DateTimeOffset jetzt)
    {
        if (fahrzeug.ChargerConnected != true)
        {
            return false;
        }

        // No measurement time, no judging the report. Both connectors always send one; a payload
        // without it is from something that is not one of them.
        if (Messzeitpunkt(fahrzeug) is not DateTimeOffset gemessen)
        {
            return false;
        }

        // The box clock reports "timeQ": 0 and may be arbitrarily wrong, so a session start
        // taken from it cannot carry an exactness test.
        if (ziel.Status.SitzungsBeginn is DateTimeOffset beginn && !ziel.Status.SitzungsBeginnAusBoxZeit)
        {
            return gemessen + sitzungsbeginnToleranz >= beginn;
        }

        return jetzt - gemessen <= maxFahrzeugAlter;
    }

    /// <summary>
    /// The state of one box, or null when it says nothing usable: no state, a state too old to
    /// act on, or no running session. In all three cases the box is left out of the result and
    /// its retained statement stays where it is.
    /// </summary>
    private BoxLage? Lage(
        string name,
        WallboxStatus status,
        IReadOnlyDictionary<string, FahrzeugZuordnung> letzteAussage,
        DateTimeOffset jetzt)
    {
        if (jetzt - status.Zeitpunkt > maxWallboxAlter)
        {
            return null;
        }

        if (status.SitzungsId is not int sitzung)
        {
            return null;
        }

        var aussage = letzteAussage.GetValueOrDefault(name);
        return new BoxLage
        {
            Name = name,
            Status = status,
            SitzungsId = sitzung,
            // The same retained message is either the running assignment or the history, and the
            // session id is what tells the two apart. That is also what makes the history survive
            // a restart without any storage of our own.
            Aktuell = aussage?.SitzungsId == sitzung ? aussage : null,
            HistorieFahrzeug = aussage?.SitzungsId == sitzung ? null : aussage?.Fahrzeug,
        };
    }

    /// <summary>The vehicle a person named for the session currently running at this box.</summary>
    private static string? Korrektur(
        IReadOnlyDictionary<string, FahrzeugZuordnung> korrekturen,
        BoxLage lage)
    {
        var korrektur = korrekturen.GetValueOrDefault(lage.Name);
        if (korrektur?.SitzungsId != lage.SitzungsId)
        {
            // Includes the expiry: an override from the previous session simply stops matching
            // when the plug is pulled, so nobody has to withdraw it.
            return null;
        }

        return string.IsNullOrWhiteSpace(korrektur.Fahrzeug) ? null : korrektur.Fahrzeug.Trim();
    }

    private static FahrzeugZuordnung Aussage(BoxLage lage, string? fahrzeug, Vertrauensgrad grad, DateTimeOffset jetzt)
        => new()
        {
            Zeitpunkt = jetzt,
            SitzungsId = lage.SitzungsId,
            Fahrzeug = fahrzeug,
            Vertrauen = grad,
        };

    private static Vertrauensgrad Grad(FahrzeugZuordnung? zuordnung)
        => zuordnung?.Vertrauen ?? Vertrauensgrad.Unbekannt;

    /// <summary>
    /// When the vehicle measured its values, as an instant. <c>LastUpdate</c> and not
    /// <c>Zeitpunkt</c>: the latter is when the connector last published, which is a statement
    /// about the connector and not about the car.
    /// </summary>
    private static DateTimeOffset? Messzeitpunkt(CarStatusData fahrzeug)
    {
        if (fahrzeug.LastUpdate is not DateTime zeit)
        {
            return null;
        }

        return zeit.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(zeit, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(zeit),
            // Both connectors write ISO 8601 in UTC; a value that lost its zone on the way is
            // read as UTC rather than as the local time of whatever machine this runs on.
            _ => new DateTimeOffset(DateTime.SpecifyKind(zeit, DateTimeKind.Utc), TimeSpan.Zero),
        };
    }

    private sealed class BoxLage
    {
        public required string Name { get; init; }
        public required WallboxStatus Status { get; init; }
        public required int SitzungsId { get; init; }
        public FahrzeugZuordnung? Aktuell { get; init; }
        public string? HistorieFahrzeug { get; init; }
        public FahrzeugZuordnung? Ergebnis { get; set; }
    }
}
