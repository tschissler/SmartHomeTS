using FluentAssertions;
using RulesEngine.Rules;
using SharedContracts;

namespace RulesEngineTests;

/// <summary>
/// The assignment rule fails silently — a wrong vehicle on a tile looks exactly like a right
/// one, and nobody notices until a kilowatt hour balance is read months later. These tests are
/// therefore the only place where every level of evidence, every transition between them and
/// every session boundary is actually exercised; the running system cannot show any of it.
/// </summary>
public class VehicleAssignmentRuleTests
{
    private const string Garage = LadeTopics.Garage;
    private const string Stellplatz = LadeTopics.Stellplatz;
    private const string Bmw = FahrzeugTopics.Bmw;
    private const string Mini = FahrzeugTopics.Mini;
    private const string Vw = FahrzeugTopics.Vw;

    private static readonly DateTimeOffset Jetzt = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sitzungsbeginn = Jetzt.AddHours(-1);

    private readonly VehicleAssignmentRule rule = new();

    // ---------------------------------------------------------------- Stufe 5: unbekannt

    [Fact]
    public void Unbekannt_WhenNothingIsKnownAboutTheRunningSession()
    {
        var ergebnis = Evaluate(Boxen((Garage, Sitzung(1))));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
        ergebnis[Garage].Fahrzeug.Should().BeNull();
        ergebnis[Garage].SitzungsId.Should().Be(1);
        ergebnis[Garage].Zeitpunkt.Should().Be(Jetzt);
    }

    [Fact]
    public void SaysNothingAboutABoxWithoutASession()
    {
        var ergebnis = Evaluate(Boxen((Garage, KeineSitzung())));

        // Absent, not "unbekannt": the retained statement of the last session has to stay
        // standing, it is the only history there is.
        ergebnis.Should().NotContainKey(Garage);
    }

    [Fact]
    public void SaysNothingAboutABoxWhoseStateHasGoneStale()
    {
        var alt = Sitzung(1) with { Zeitpunkt = Jetzt - VehicleAssignmentRule.MaxWallboxAlter.Add(TimeSpan.FromSeconds(1)) };

        var ergebnis = Evaluate(Boxen((Garage, alt)));

        // A missing connector is not evidence that the plug was pulled.
        ergebnis.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- Stufe 4: Historie

    [Fact]
    public void Vermutet_TakesTheVehicleOfThePreviousSessionAtThisBox()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(2))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Bmw, Vertrauensgrad.Erkannt))));

        ergebnis[Garage].Fahrzeug.Should().Be(Bmw);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Vermutet, "a new session inherits only a guess");
        ergebnis[Garage].SitzungsId.Should().Be(2, "the guess is about the session running now");
    }

    [Fact]
    public void Vermutet_DoesNotCarryTheVehicleOfTheOtherBox()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(2)), (Stellplatz, KeineSitzung())),
            letzteAussage: Aussagen((Stellplatz, Zuordnung(1, Bmw, Vertrauensgrad.Bestaetigt))));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
        ergebnis[Garage].Fahrzeug.Should().BeNull();
    }

    [Fact]
    public void Vermutet_IgnoresAPreviousSessionThatNamedNobody()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(2))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, null, Vertrauensgrad.Unbekannt))));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    // ---------------------------------------------------------------- Stufe 2: eine Box belegt

    [Fact]
    public void Erkannt_WhenAVehicleReportsAndOneBoxIsOccupiedAndUnassigned()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1)), (Stellplatz, KeineSitzung())),
            Fahrzeuge((Vw, Meldet(true))));

        ergebnis[Garage].Fahrzeug.Should().Be(Vw);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
    }

    [Fact]
    public void Erkannt_IsNotBlockedByOtherVehiclesReportingNothing()
    {
        // The core of the concept: BMW and Mini say "not connected" - one of them has been
        // silent for 35 days - and that must change nothing either way.
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge(
                (Bmw, Meldet(false, gemessen: Jetzt.AddDays(-35))),
                (Mini, Meldet(false)),
                (Vw, Meldet(true))));

        ergebnis[Garage].Fahrzeug.Should().Be(Vw);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
    }

    [Fact]
    public void Unbekannt_WhenTwoVehiclesReportConnectedForOneBox()
    {
        // One of the two is charging somewhere else. Which one cannot be told, so the rule
        // does not pretend to know.
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Mini, Meldet(true)), (Vw, Meldet(true))));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    [Fact]
    public void NeverConcludesFromSilence_EvenWhenOnlyOneVehicleCouldPossiblyBeIt()
    {
        // Two of three vehicles positively say "not connected". Exclusion would name the third
        // with conviction; the measured case of 2026-09-20 shows it would be wrong.
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Bmw, Meldet(false)), (Mini, Meldet(false)), (Vw, Meldet(null))));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    // ---------------------------------------------------------------- Stufe 3: zwei Boxen belegt

    [Fact]
    public void Erkannt_PlacesASecondVehicleAtTheOtherOccupiedBox()
    {
        // Garage is firmly taken, the Stellplatz is occupied too, and the Mini reports in: the
        // only box left for it is the Stellplatz. That is an exclusion over the measured
        // occupancy of the boxes, not over anybody's silence.
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1)), (Stellplatz, Sitzung(2))),
            Fahrzeuge((Mini, Meldet(true))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Vw, Vertrauensgrad.Erkannt))));

        ergebnis[Stellplatz].Fahrzeug.Should().Be(Mini);
        ergebnis[Stellplatz].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
        ergebnis[Garage].Fahrzeug.Should().Be(Vw, "the assignment that was already there sticks");
    }

    [Fact]
    public void Vermutet_WhenBothOccupiedBoxesAreStillUnassigned()
    {
        // "Ist noch keine der beiden Boxen vergeben, bleibt es bei Stufe 4."
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1)), (Stellplatz, Sitzung(2))),
            Fahrzeuge((Mini, Meldet(true))),
            letzteAussage: Aussagen(
                (Garage, Zuordnung(90, Bmw, Vertrauensgrad.Erkannt)),
                (Stellplatz, Zuordnung(91, Vw, Vertrauensgrad.Erkannt))));

        ergebnis[Garage].Should().BeEquivalentTo(Zuordnung(1, Bmw, Vertrauensgrad.Vermutet, Jetzt));
        ergebnis[Stellplatz].Should().BeEquivalentTo(Zuordnung(2, Vw, Vertrauensgrad.Vermutet, Jetzt));
    }

    [Fact]
    public void DoesNotPlaceAVehicleThatIsAlreadyFirmlyAtTheOtherBox()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1)), (Stellplatz, Sitzung(2))),
            Fahrzeuge((Vw, Meldet(true))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Vw, Vertrauensgrad.Bestaetigt))));

        ergebnis[Stellplatz].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt, "the VW cannot be at both boxes");
        ergebnis[Garage].Fahrzeug.Should().Be(Vw);
    }

    [Fact]
    public void DoesNotPlaceAVehicleWhileAnotherBoxHasGoneStale()
    {
        // An unreporting box has unknown occupancy. Concluding "then it must be the other one"
        // would be exclusion again - over a silent box instead of a silent car.
        var stumm = Sitzung(2) with { Zeitpunkt = Jetzt.AddHours(-1) };

        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1)), (Stellplatz, stumm)),
            Fahrzeuge((Vw, Meldet(true))));

        ergebnis.Should().NotContainKey(Stellplatz);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    // ---------------------------------------------------- Alter der Fahrzeugmeldung

    [Fact]
    public void IgnoresAPositiveReportMeasuredBeforeThePlugWentIn()
    {
        // The vehicle was connected - to something else, an hour before this session started.
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Vw, Meldet(true, gemessen: Sitzungsbeginn.AddHours(-1)))));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    [Fact]
    public void AcceptsAPositiveReportThatArrivesLongAfterThePlugWentIn()
    {
        // The VW polls every 15 minutes and its data is another hour behind on top, so its
        // report about this session lands well after the plug did. That has to work, otherwise
        // level 2 would never fire for the VW at all.
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Vw, Meldet(true, gemessen: Sitzungsbeginn.AddMinutes(20)))));

        ergebnis[Garage].Fahrzeug.Should().Be(Vw);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
    }

    [Fact]
    public void FallsBackToAnAgeWindowWhenTheSessionStartCameFromTheBoxClock()
    {
        // "timeQ": 0 - the box clock may be arbitrarily wrong, so it cannot carry an exactness
        // test. A five week old report is still rejected.
        var boxZeit = Sitzung(1) with { SitzungsBeginn = Jetzt.AddYears(-3), SitzungsBeginnAusBoxZeit = true };

        var frisch = Evaluate(Boxen((Garage, boxZeit)), Fahrzeuge((Vw, Meldet(true, Jetzt.AddMinutes(-30)))));
        var uralt = Evaluate(Boxen((Garage, boxZeit)), Fahrzeuge((Vw, Meldet(true, Jetzt.AddDays(-35)))));

        frisch[Garage].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
        uralt[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    [Fact]
    public void IgnoresAPositiveReportWithoutAMeasurementTime()
    {
        var ohneZeit = new CarStatusData { ChargerConnected = true, LastUpdate = null, Zeitpunkt = Jetzt };

        var ergebnis = Evaluate(Boxen((Garage, Sitzung(1))), Fahrzeuge((Vw, ohneZeit)));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    [Fact]
    public void JudgesTheVehicleByItsMeasurementTimeNotByWhenTheConnectorPublished()
    {
        // A connector republishes on every event while the values inside stay old. Zeitpunkt
        // says the connector is alive, lastUpdate says how old the statement is.
        var frischVeroeffentlicht = Meldet(true, gemessen: Jetzt.AddDays(-35));
        frischVeroeffentlicht.Zeitpunkt = Jetzt;

        var ergebnis = Evaluate(Boxen((Garage, Sitzung(1))), Fahrzeuge((Vw, frischVeroeffentlicht)));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    // ---------------------------------------------------------------- Stufe 1: Override

    [Fact]
    public void Bestaetigt_WhenTheOverrideIsForTheRunningSession()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            korrekturen: Aussagen((Garage, Zuordnung(1, Mini, Vertrauensgrad.Bestaetigt))));

        ergebnis[Garage].Fahrzeug.Should().Be(Mini);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Bestaetigt);
    }

    [Fact]
    public void OverrideBeatsAPositiveVehicleReport()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Vw, Meldet(true))),
            korrekturen: Aussagen((Garage, Zuordnung(1, Mini, Vertrauensgrad.Bestaetigt))));

        ergebnis[Garage].Fahrzeug.Should().Be(Mini);
    }

    [Fact]
    public void OverrideCorrectsAnEarlierOverride()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            korrekturen: Aussagen((Garage, Zuordnung(1, Mini, Vertrauensgrad.Bestaetigt))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Bmw, Vertrauensgrad.Bestaetigt))));

        // Only a person ever produces "bestaetigt", so replacing one can only be a second person.
        ergebnis[Garage].Fahrzeug.Should().Be(Mini);
    }

    [Fact]
    public void OverrideExpiresWithTheSession()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(2))),
            korrekturen: Aussagen((Garage, Zuordnung(1, Mini, Vertrauensgrad.Bestaetigt))));

        // Nobody withdrew it; its session simply no longer matches.
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    [Fact]
    public void ExpiredOverrideDoesNotEvenServeAsHistory()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(2))),
            korrekturen: Aussagen((Garage, Zuordnung(1, Mini, Vertrauensgrad.Bestaetigt))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Bmw, Vertrauensgrad.Bestaetigt))));

        // The history comes from the rule's own topic, never from the correction topic.
        ergebnis[Garage].Fahrzeug.Should().Be(Bmw);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Vermutet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IgnoresAnOverrideThatNamesNoVehicle(string? fahrzeug)
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Vw, Meldet(true))),
            korrekturen: Aussagen((Garage, Zuordnung(1, fahrzeug, Vertrauensgrad.Bestaetigt))));

        ergebnis[Garage].Fahrzeug.Should().Be(Vw);
    }

    // ---------------------------------------------------------------- Aufwertung und Kleben

    [Fact]
    public void UpgradesFromVermutetToErkannt()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Vw, Meldet(true))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Vw, Vertrauensgrad.Vermutet))));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
        ergebnis[Garage].Fahrzeug.Should().Be(Vw);
    }

    [Fact]
    public void UpgradeMayAlsoCorrectTheVehicle()
    {
        // The history guess was wrong. Positive evidence outranks it, and the grade rises with
        // the correction - this is an upgrade, not a silent overturn of an equal statement.
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Mini, Meldet(true))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Bmw, Vertrauensgrad.Vermutet))));

        ergebnis[Garage].Fahrzeug.Should().Be(Mini);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
    }

    [Fact]
    public void ErkanntIsNeverOverturnedByAnotherVehiclesReport()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Mini, Meldet(true))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Vw, Vertrauensgrad.Erkannt))));

        ergebnis[Garage].Fahrzeug.Should().Be(Vw, "an assignment is only ever raised, never swapped sideways");
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
    }

    [Fact]
    public void ErkanntSurvivesTheVehicleFallingSilentForTheRestOfTheSession()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Vw, Meldet(false))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Vw, Vertrauensgrad.Erkannt))));

        ergebnis[Garage].Fahrzeug.Should().Be(Vw);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Erkannt);
    }

    [Fact]
    public void BestaetigtIsNeverLoweredByAnyAutomaticEvidence()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(1))),
            Fahrzeuge((Mini, Meldet(true))),
            letzteAussage: Aussagen((Garage, Zuordnung(1, Bmw, Vertrauensgrad.Bestaetigt))));

        ergebnis[Garage].Fahrzeug.Should().Be(Bmw);
        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Bestaetigt);
    }

    // ---------------------------------------------------------------- Sitzungswechsel

    [Fact]
    public void ANewSessionDropsBackToAGuess()
    {
        var alt = Evaluate(
            Boxen((Garage, Sitzung(1))),
            korrekturen: Aussagen((Garage, Zuordnung(1, Mini, Vertrauensgrad.Bestaetigt))));

        var neu = Evaluate(
            Boxen((Garage, Sitzung(2))),
            korrekturen: Aussagen((Garage, Zuordnung(1, Mini, Vertrauensgrad.Bestaetigt))),
            letzteAussage: Aussagen((Garage, alt[Garage])));

        alt[Garage].Vertrauen.Should().Be(Vertrauensgrad.Bestaetigt);
        neu[Garage].Vertrauen.Should().Be(Vertrauensgrad.Vermutet, "automatic plus history, no forgotten permanent state");
        neu[Garage].Fahrzeug.Should().Be(Mini);
    }

    [Fact]
    public void TheWholeLifeOfOneSessionAndTheNext()
    {
        // Unplugged, plugged in with a history, the vehicle reports, the plug is pulled, a
        // different vehicle plugs in - stepped through the way the service will see it.
        var stand = new Dictionary<string, FahrzeugZuordnung>
        {
            [Garage] = Zuordnung(1, Bmw, Vertrauensgrad.Erkannt),
        };

        var frei = Evaluate(Boxen((Garage, KeineSitzung())), letzteAussage: stand);
        frei.Should().BeEmpty("a free box keeps its old statement standing as the history");

        var angesteckt = Evaluate(Boxen((Garage, Sitzung(2))), letzteAussage: stand);
        angesteckt[Garage].Should().BeEquivalentTo(Zuordnung(2, Bmw, Vertrauensgrad.Vermutet, Jetzt));
        stand[Garage] = angesteckt[Garage];

        var gemeldet = Evaluate(Boxen((Garage, Sitzung(2))), Fahrzeuge((Bmw, Meldet(true))), letzteAussage: stand);
        gemeldet[Garage].Should().BeEquivalentTo(Zuordnung(2, Bmw, Vertrauensgrad.Erkannt, Jetzt));
        stand[Garage] = gemeldet[Garage];

        var abgesteckt = Evaluate(Boxen((Garage, KeineSitzung())), letzteAussage: stand);
        abgesteckt.Should().BeEmpty();

        var naechste = Evaluate(Boxen((Garage, Sitzung(3))), Fahrzeuge((Mini, Meldet(true))), letzteAussage: stand);
        naechste[Garage].Should().BeEquivalentTo(Zuordnung(3, Mini, Vertrauensgrad.Erkannt, Jetzt));
    }

    // ---------------------------------------------------------------- Neustart

    [Fact]
    public void RestartMidSessionRestoresEverythingFromTheRetainedTopicsAlone()
    {
        // Everything arrives as a retained burst: assignment, box state, override, vehicle. None
        // of it is a fresh event, and the rule must not treat the burst as news.
        var garage = Zuordnung(7, Vw, Vertrauensgrad.Erkannt);
        var stellplatz = Zuordnung(8, Bmw, Vertrauensgrad.Bestaetigt);

        var ergebnis = Evaluate(
            Boxen((Garage, Sitzung(7)), (Stellplatz, Sitzung(8))),
            Fahrzeuge((Bmw, Meldet(false, gemessen: Jetzt.AddDays(-35)))),
            korrekturen: Aussagen((Stellplatz, stellplatz)),
            letzteAussage: Aussagen((Garage, garage), (Stellplatz, stellplatz)));

        // Unchanged down to the timestamp: a restart produces no publication at all, so nothing
        // in the charging log looks like it was re-decided at rollout time.
        ergebnis[Garage].Should().BeEquivalentTo(garage);
        ergebnis[Stellplatz].Should().BeEquivalentTo(stellplatz);
    }

    [Fact]
    public void RestartAfterTheSessionEndedLeavesTheHistoryUntouched()
    {
        var ergebnis = Evaluate(
            Boxen((Garage, KeineSitzung())),
            letzteAussage: Aussagen((Garage, Zuordnung(7, Vw, Vertrauensgrad.Bestaetigt))));

        ergebnis.Should().BeEmpty();
    }

    [Fact]
    public void RestartWithoutAnyRetainedAssignmentStartsAtUnbekannt()
    {
        // First commissioning: there is no history yet, and the running session is simply
        // unknown until a vehicle reports or somebody clicks.
        var ergebnis = Evaluate(Boxen((Garage, Sitzung(1)), (Stellplatz, Sitzung(2))));

        ergebnis[Garage].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
        ergebnis[Stellplatz].Vertrauen.Should().Be(Vertrauensgrad.Unbekannt);
    }

    // ---------------------------------------------------------------- Republish-Schutz

    [Fact]
    public void RepeatedEvaluationsLeaveTheStatementCompletelyUntouched()
    {
        // The topic is retained and the rule runs several times a minute. An unchanged
        // assignment must come out unchanged down to its timestamp, so the field reads as
        // "assigned since" and the retained topic is not rewritten every few seconds.
        var eingang = Boxen((Garage, Sitzung(1)));
        var autos = Fahrzeuge((Vw, Meldet(true)));

        var erste = Evaluate(eingang, autos);
        var zweite = Evaluate(eingang, autos, letzteAussage: Aussagen((Garage, erste[Garage])), jetzt: Jetzt.AddSeconds(5));

        zweite[Garage].Should().BeEquivalentTo(erste[Garage]);
    }

    [Fact]
    public void AStandingOverrideDoesNotRewriteItselfOnEveryEvaluation()
    {
        // The override stands retained for the whole session, so its branch is taken every
        // single time. Without carrying the existing statement forward this would republish a
        // new timestamp onto a retained topic several times a minute.
        var eingang = Boxen((Garage, Sitzung(1)));
        var korrektur = Aussagen((Garage, Zuordnung(1, Mini, Vertrauensgrad.Bestaetigt)));

        var erste = Evaluate(eingang, korrekturen: korrektur);
        var zweite = Evaluate(eingang, korrekturen: korrektur,
            letzteAussage: Aussagen((Garage, erste[Garage])), jetzt: Jetzt.AddSeconds(5));

        zweite[Garage].Should().BeEquivalentTo(erste[Garage]);
    }

    [Fact]
    public void GleicheAussageWie_SeparatesTheThreeFieldsThatMatter()
    {
        var basis = Zuordnung(1, Bmw, Vertrauensgrad.Erkannt, Jetzt);

        basis.GleicheAussageWie(null).Should().BeFalse();
        basis.GleicheAussageWie(basis with { Zeitpunkt = Jetzt.AddHours(3) }).Should().BeTrue();
        basis.GleicheAussageWie(basis with { SitzungsId = 2 }).Should().BeFalse();
        basis.GleicheAussageWie(basis with { Fahrzeug = Mini }).Should().BeFalse();
        basis.GleicheAussageWie(basis with { Vertrauen = Vertrauensgrad.Bestaetigt }).Should().BeFalse();
    }

    // ---------------------------------------------------------------- Payload

    [Fact]
    public void TheConfidenceTravelsOnTheWireAsTheInfluxFieldSpellsIt()
    {
        // Docs/Ladeprotokoll.md fixes the values of the "vertrauen" field, and the Grafana
        // filter vertrauen IN ('bestaetigt','erkannt') reads them unchanged.
        var json = System.Text.Json.JsonSerializer.Serialize(Zuordnung(1, Bmw, Vertrauensgrad.Bestaetigt, Jetzt));

        json.Should().Contain("\"Vertrauen\":\"bestaetigt\"");
        System.Text.Json.JsonSerializer.Deserialize<FahrzeugZuordnung>(json)!.Vertrauen
            .Should().Be(Vertrauensgrad.Bestaetigt);
    }

    // ---------------------------------------------------------------- Hilfsmittel

    private IReadOnlyDictionary<string, FahrzeugZuordnung> Evaluate(
        IReadOnlyDictionary<string, WallboxStatus> wallboxen,
        IReadOnlyDictionary<string, CarStatusData>? fahrzeuge = null,
        IReadOnlyDictionary<string, FahrzeugZuordnung>? korrekturen = null,
        IReadOnlyDictionary<string, FahrzeugZuordnung>? letzteAussage = null,
        DateTimeOffset? jetzt = null)
        => rule.Evaluate(
            wallboxen,
            fahrzeuge ?? new Dictionary<string, CarStatusData>(),
            korrekturen ?? new Dictionary<string, FahrzeugZuordnung>(),
            letzteAussage ?? new Dictionary<string, FahrzeugZuordnung>(),
            jetzt ?? Jetzt);

    private static Dictionary<string, WallboxStatus> Boxen(params (string Name, WallboxStatus Status)[] boxen)
        => boxen.ToDictionary(b => b.Name, b => b.Status);

    private static Dictionary<string, CarStatusData> Fahrzeuge(params (string Name, CarStatusData Daten)[] autos)
        => autos.ToDictionary(a => a.Name, a => a.Daten);

    private static Dictionary<string, FahrzeugZuordnung> Aussagen(params (string Box, FahrzeugZuordnung Aussage)[] aussagen)
        => aussagen.ToDictionary(a => a.Box, a => a.Aussage);

    private static WallboxStatus Sitzung(int id) => new()
    {
        Zeitpunkt = Jetzt,
        PlugStatus = WallboxStatus.PlugStatusFahrzeugVerbunden,
        DeviceState = 3,
        SitzungsId = id,
        SitzungsBeginn = Sitzungsbeginn,
    };

    private static WallboxStatus KeineSitzung() => new()
    {
        Zeitpunkt = Jetzt,
        PlugStatus = 3,
        DeviceState = 1,
    };

    private static CarStatusData Meldet(bool? verbunden, DateTimeOffset? gemessen = null) => new()
    {
        ChargerConnected = verbunden,
        LastUpdate = (gemessen ?? Sitzungsbeginn.AddMinutes(5)).UtcDateTime,
        Zeitpunkt = gemessen ?? Sitzungsbeginn.AddMinutes(5),
    };

    private static FahrzeugZuordnung Zuordnung(int? sitzung, string? fahrzeug, Vertrauensgrad grad, DateTimeOffset? zeitpunkt = null)
        => new()
        {
            Zeitpunkt = zeitpunkt ?? Jetzt.AddHours(-2),
            SitzungsId = sitzung,
            Fahrzeug = fahrzeug,
            Vertrauen = grad,
        };
}
