using FluentAssertions;
using SharedContracts;
using SmartHome.DataHub;
using System.Text.Json;

namespace SmartHome.DataHubTests;

/// <summary>
/// The join of the two session topics: when a row of <c>ladesitzungen</c> is made, and — more
/// importantly — when it is not. Both inputs are retained, so both arrive again after every
/// restart and in any order; that is what most of these tests are about.
/// </summary>
public class LadesitzungenTests
{
    private static readonly DateTimeOffset Beginn = new(2026, 9, 20, 18, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset Ende = Beginn.AddHours(12);

    private static Ladesitzung Sitzung(
        int id = 5,
        Ladesitzungszustand zustand = Ladesitzungszustand.Beendet,
        DateTimeOffset? beginn = null,
        DateTimeOffset? ende = null,
        decimal pv = 2m,
        decimal batterie = 1m,
        decimal netz = 0.5m,
        decimal box = 3.5m,
        long ladezeit = 7200,
        Beginnquelle quelle = Beginnquelle.Steckflanke)
        => new()
        {
            Zeitpunkt = ende ?? Ende,
            SitzungsId = id,
            Beginn = beginn ?? Beginn,
            Beginnquelle = quelle,
            Ende = zustand == Ladesitzungszustand.Beendet ? ende ?? Ende : null,
            Zustand = zustand,
            EnergiePvKwh = pv,
            EnergieBatterieKwh = batterie,
            EnergieNetzKwh = netz,
            EnergieBoxKwh = box,
            LadezeitSekunden = ladezeit,
        };

    private static FahrzeugZuordnung Zuordnung(
        int? id = 5, string? fahrzeug = "BMW", Vertrauensgrad vertrauen = Vertrauensgrad.Erkannt)
        => new() { Zeitpunkt = Ende, SitzungsId = id, Fahrzeug = fahrzeug, Vertrauen = vertrauen };

    // ---------------------------------------------------------------- the row itself

    [Fact]
    public void EinePassendeSitzungWirdVollstaendigGeschrieben()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        var ergebnis = buch.MeldeSitzung(LadeTopics.Garage, Sitzung());

        ergebnis.Art.Should().Be(Zusammenfuehrung.Geschrieben);
        var satz = ergebnis.Satz!;
        satz.Wallbox.Should().Be(LadeTopics.Garage);
        satz.Beginn.Should().Be(Beginn, "the session start is the time axis of the row");
        satz.Ende.Should().Be(Ende);
        satz.SitzungsId.Should().Be(5);
        satz.Fahrzeug.Should().Be("BMW");
        satz.Vertrauen.Should().Be("erkannt");
        satz.Beginnquelle.Should().Be("steckflanke");
        satz.EnergieKwh.Should().Be(3.5m);
        satz.EnergiePvKwh.Should().Be(2m);
        satz.EnergieBatterieKwh.Should().Be(1m);
        satz.EnergieNetzKwh.Should().Be(0.5m);
        satz.EnergieUnzugeordnetKwh.Should().Be(0m);
    }

    [Theory]
    [InlineData(Beginnquelle.Steckflanke, "steckflanke")]
    [InlineData(Beginnquelle.Regelzyklus, "regelzyklus")]
    [InlineData(Beginnquelle.Boxuhr, "boxuhr")]
    [InlineData(Beginnquelle.Dienstanlauf, "dienstanlauf")]
    [InlineData(Beginnquelle.Unbekannt, "unbekannt")]
    public void DieHerkunftDesBeginnsStehtInDerZeile(Beginnquelle quelle, string erwartet)
    {
        // dauer_s wird aus dem Beginn gerechnet. Ohne diese Spalte kann niemand eine gemessene
        // Steckdauer von einer geschätzten unterscheiden — und eine geschätzte sieht genauso
        // solide aus wie eine gemessene.
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        var satz = buch.MeldeSitzung(LadeTopics.Garage, Sitzung(quelle: quelle)).Satz!;

        satz.Beginnquelle.Should().Be(erwartet);
    }

    [Theory]
    [InlineData(Beginnquelle.Unbekannt)]
    [InlineData(Beginnquelle.Steckflanke)]
    [InlineData(Beginnquelle.Regelzyklus)]
    [InlineData(Beginnquelle.Boxuhr)]
    [InlineData(Beginnquelle.Dienstanlauf)]
    public void DerDrahtnameDerBeginnquelleIstDerselbeWieImJsonPayload(Beginnquelle quelle)
    {
        // Wie bei vertrauen: die Spalte und der Payload müssen dasselbe Wort tragen, sonst
        // filtert ein Grafana-Panel still ins Leere.
        var ausDemPayload = JsonSerializer.Serialize(quelle).Trim('"');

        Beginnquellen.Drahtname(quelle).Should().Be(ausDemPayload);
    }

    [Fact]
    public void SteckdauerUndLadezeitSindZweiVerschiedeneZahlen()
    {
        // Twelve hours on the box, two hours of surplus charging. Without the separation every
        // surplus charge looks like an absurdly low average power.
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        var satz = buch.MeldeSitzung(LadeTopics.Garage, Sitzung()).Satz!;

        satz.DauerSekunden.Should().Be(12 * 3600);
        satz.LadezeitSekunden.Should().Be(7200);
    }

    [Fact]
    public void DieUnzugeordneteEnergieDarfNegativSeinUndWirdNichtGeklemmt()
    {
        // Rounding and the one cycle by which the meters trail the box produce this. A small
        // negative number is honest; a Math.Max(0, …) would hide exactly the thing that shows
        // how good the attribution is.
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        var satz = buch.MeldeSitzung(LadeTopics.Garage,
            Sitzung(pv: 2m, batterie: 1m, netz: 0.5m, box: 3.494m)).Satz!;

        satz.EnergieUnzugeordnetKwh.Should().Be(-0.006m);
    }

    [Fact]
    public void EineLuecke_in_der_Zurechnung_bleibt_stehen()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        var satz = buch.MeldeSitzung(LadeTopics.Garage,
            Sitzung(pv: 1m, batterie: 0m, netz: 0m, box: 4m)).Satz!;

        satz.EnergieUnzugeordnetKwh.Should().Be(3m,
            "the controller was not running for part of this charge, and that is information, not an error to scale away");
    }

    [Fact]
    public void OhneFahrzeugStehtEinStrichUndKeinLeererText()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung(fahrzeug: null, vertrauen: Vertrauensgrad.Unbekannt));

        var satz = buch.MeldeSitzung(LadeTopics.Garage, Sitzung()).Satz!;

        satz.Fahrzeug.Should().Be("-");
        satz.Vertrauen.Should().Be("unbekannt");
    }

    // ---------------------------------------------------------------- when nothing is written

    [Fact]
    public void EineLaufendeSitzungErzeugtKeineZeile()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        var ergebnis = buch.MeldeSitzung(LadeTopics.Garage, Sitzung(zustand: Ladesitzungszustand.Laufend));

        ergebnis.Art.Should().Be(Zusammenfuehrung.SitzungLaeuftNoch);
        ergebnis.Satz.Should().BeNull("the record is made at the end of the session, not during it");
    }

    [Fact]
    public void EineAbweichendeSitzungsIdSchreibtNichts()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung(id: 4));

        var ergebnis = buch.MeldeSitzung(LadeTopics.Garage, Sitzung(id: 5));

        ergebnis.Art.Should().Be(Zusammenfuehrung.SitzungsIdPasstNicht);
        ergebnis.Satz.Should().BeNull();
        ergebnis.Begruendung.Should().Contain("5").And.Contain("4",
            "the log line has to name both sides, otherwise nobody can tell which one is behind");
    }

    [Fact]
    public void OhneZuordnungWirdNichtsGeschrieben()
    {
        var buch = new Ladesitzungen();

        var ergebnis = buch.MeldeSitzung(LadeTopics.Garage, Sitzung());

        ergebnis.Art.Should().Be(Zusammenfuehrung.ZuordnungFehlt);
        ergebnis.Satz.Should().BeNull();
    }

    [Fact]
    public void OhneBeginnWirdNichtsGeschrieben()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        var ergebnis = buch.MeldeSitzung(LadeTopics.Garage, Sitzung() with { Beginn = null });

        ergebnis.Art.Should().Be(Zusammenfuehrung.ZeitenUnbrauchbar,
            "the session start is the time of the row; without it there is nowhere to put it");
    }

    [Fact]
    public void EineSitzungDieVorIhremBeginnEndetWirdNichtGeschrieben()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        var ergebnis = buch.MeldeSitzung(LadeTopics.Garage, Sitzung(ende: Beginn.AddHours(-1)));

        ergebnis.Art.Should().Be(Zusammenfuehrung.ZeitenUnbrauchbar);
    }

    // ---------------------------------------------------------------- retained replay

    [Fact]
    public void DieZuordnungDarfNachDerSitzungKommen()
    {
        // Both topics are retained and the broker replays them in whatever order it likes.
        var buch = new Ladesitzungen();
        buch.MeldeSitzung(LadeTopics.Garage, Sitzung()).Art.Should().Be(Zusammenfuehrung.ZuordnungFehlt);

        var ergebnis = buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());

        ergebnis.Art.Should().Be(Zusammenfuehrung.Geschrieben);
        ergebnis.Satz!.Fahrzeug.Should().Be("BMW");
    }

    [Fact]
    public void DieselbeSitzungWirdNurEinmalGeschrieben()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());
        buch.MeldeSitzung(LadeTopics.Garage, Sitzung()).Art.Should().Be(Zusammenfuehrung.Geschrieben);

        buch.MeldeSitzung(LadeTopics.Garage, Sitzung()).Art.Should().Be(Zusammenfuehrung.BereitsGeschrieben);
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung()).Art.Should().Be(Zusammenfuehrung.BereitsGeschrieben);
    }

    [Fact]
    public void EineBereitsGeschriebeneSitzungOhneZuordnungBeschwertSichNicht()
    {
        // Otherwise every reconnect would produce a warning about a record that is long written.
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());
        buch.MeldeSitzung(LadeTopics.Garage, Sitzung());

        buch.MeldeSitzung(LadeTopics.Garage, Sitzung()).Art.Should().Be(Zusammenfuehrung.BereitsGeschrieben);
    }

    [Fact]
    public void DieNaechsteSitzungDerselbenBoxWirdWiederGeschrieben()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung());
        buch.MeldeSitzung(LadeTopics.Garage, Sitzung());

        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung(id: 6, fahrzeug: "Mini"));
        var ergebnis = buch.MeldeSitzung(LadeTopics.Garage,
            Sitzung(id: 6, beginn: Ende.AddHours(1), ende: Ende.AddHours(3)));

        ergebnis.Art.Should().Be(Zusammenfuehrung.Geschrieben);
        ergebnis.Satz!.Fahrzeug.Should().Be("Mini");
    }

    [Fact]
    public void DieBeidenBoxenWerdenGetrenntGefuehrt()
    {
        var buch = new Ladesitzungen();
        buch.MeldeZuordnung(LadeTopics.Garage, Zuordnung(id: 5, fahrzeug: "BMW"));
        buch.MeldeZuordnung(LadeTopics.Stellplatz, Zuordnung(id: 5, fahrzeug: "Mini"));

        buch.MeldeSitzung(LadeTopics.Garage, Sitzung(id: 5)).Satz!.Fahrzeug.Should().Be("BMW");
        buch.MeldeSitzung(LadeTopics.Stellplatz, Sitzung(id: 5)).Satz!.Fahrzeug.Should().Be("Mini",
            "same session number at two boxes is normal — the boxes count for themselves");
    }

    // ---------------------------------------------------------------- the vertrauen column

    [Theory]
    [InlineData(Vertrauensgrad.Unbekannt)]
    [InlineData(Vertrauensgrad.Vermutet)]
    [InlineData(Vertrauensgrad.Erkannt)]
    [InlineData(Vertrauensgrad.Bestaetigt)]
    public void DerDrahtnameIstDerselbeWieImJsonPayload(Vertrauensgrad grad)
    {
        // The Grafana filter vertrauen IN ('bestaetigt','erkannt') reads the column, the web
        // interface reads the payload. One spelling, or one of the two is silently empty.
        var ausDemPayload = JsonSerializer.Serialize(grad).Trim('"');

        Vertrauensgrade.Drahtname(grad).Should().Be(ausDemPayload);
    }
}
