using FluentAssertions;
using SharedContracts;
using SmartHome.DataHub;
using System.Text.Json;

namespace SmartHome.DataHubTests;

/// <summary>
/// The rules of the vehicle branch, tested without a broker, a clock or a database. The
/// workflow of this service has no test job (SmartHome.DataHub.yml builds only), so these run
/// locally — which is exactly why they exist for the part that decides what lands in the
/// history for good.
/// </summary>
public class FahrzeugdatenTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    // ---------------------------------------------------------------- Messzeit

    [Fact]
    public void Messzeit_nimmt_lastUpdate_und_nicht_den_Zeitpunkt()
    {
        var daten = new CarStatusData
        {
            Zeitpunkt = Jetzt,
            LastUpdate = new DateTime(2026, 8, 16, 9, 30, 0, DateTimeKind.Utc),
        };

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().Be(
            new DateTimeOffset(2026, 8, 16, 9, 30, 0, TimeSpan.Zero),
            "lastUpdate is when the vehicle measured; Zeitpunkt is only when the connector published");
    }

    [Fact]
    public void Messzeit_faellt_auf_den_Zeitpunkt_zurueck_wenn_lastUpdate_fehlt()
    {
        var daten = new CarStatusData { Zeitpunkt = Jetzt.AddMinutes(-5), LastUpdate = null };

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().Be(Jetzt.AddMinutes(-5));
    }

    [Fact]
    public void Messzeit_ist_null_wenn_der_Payload_gar_keine_Zeit_traegt()
    {
        var daten = new CarStatusData { Zeitpunkt = null, LastUpdate = null };

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().BeNull(
            "without a measurement time there is no place to put the point — and the arrival " +
            "time is not a substitute");
    }

    [Fact]
    public void Messzeit_verwirft_einen_Default_Zeitstempel()
    {
        var daten = new CarStatusData { LastUpdate = default(DateTime) };

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().BeNull();
    }

    [Fact]
    public void Messzeit_verwirft_eine_Messung_weit_in_der_Zukunft()
    {
        var daten = new CarStatusData { LastUpdate = Jetzt.AddHours(2).UtcDateTime };

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().BeNull();
    }

    [Fact]
    public void Messzeit_erlaubt_eine_leicht_vorgehende_Fahrzeuguhr()
    {
        var daten = new CarStatusData { LastUpdate = Jetzt.AddMinutes(10).UtcDateTime };

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().Be(Jetzt.AddMinutes(10));
    }

    [Fact]
    public void Messzeit_liest_eine_Zeit_ohne_Zonenangabe_als_UTC()
    {
        var ohneZone = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Unspecified);
        var daten = new CarStatusData { LastUpdate = ohneZone };

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().Be(
            new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Messzeit_uebernimmt_den_Offset_eines_ISO_Zeitstempels()
    {
        // This is how the VW connector writes it: RFC3339 with an explicit local offset.
        var payload = """{"lastUpdate":"2026-09-20T14:00:00+02:00"}""";
        var daten = JsonSerializer.Deserialize<CarStatusData>(payload, JsonOptions)!;

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().Be(
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
    }

    // ------------------------------------------------------------ Ladestatus

    [Theory]
    [InlineData("NOCHARGING", 0)]
    [InlineData("off", 0)]
    [InlineData("CHARGINGACTIVE", 1)]
    [InlineData("charging", 1)]
    [InlineData("CHARGINGPAUSED", 2)]
    [InlineData("CHARGINGENDED", 3)]
    [InlineData("CHARGINGERROR", 4)]
    [InlineData("ready_for_charging", 5)]
    [InlineData("conservation", 6)]
    [InlineData("discharging", 7)]
    public void Ladestatuscode_kennt_beide_Hersteller_Vokabulare(string status, int erwartet)
    {
        Fahrzeugdaten.Ladestatuscode(status).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown charging state")]
    [InlineData("irgendwas Neues")]
    public void Ladestatuscode_erfindet_keinen_Code_fuer_Unbekanntes(string? status)
    {
        Fahrzeugdaten.Ladestatuscode(status).Should().BeNull(
            "a made up number in the history means nothing, and 0 would read as 'not charging'");
    }

    // ------------------------------------------------------- Fehlende Werte

    [Fact]
    public void Ein_leerer_Payload_erzeugt_keinen_einzigen_Punkt()
    {
        var records = Fahrzeugdaten.NachInfluxRecords("BMW", new CarStatusData());

        records.Should().BeEmpty("a missing value is not a zero, and a zero is a measurement");
    }

    [Fact]
    public void Nur_die_gemeldeten_Werte_werden_geschrieben()
    {
        var daten = new CarStatusData { Battery = 62, Mileage = 41234.5 };

        Fahrzeugdaten.NachInfluxRecords("Mini", daten)
            .Select(r => r.Measurement)
            .Should().BeEquivalentTo("Ladestand", "Kilometerstand");
    }

    [Fact]
    public void Ein_Ladestand_von_null_Prozent_ist_eine_Messung_und_wird_geschrieben()
    {
        var daten = new CarStatusData { Battery = 0 };

        Fahrzeugdaten.NachInfluxRecords("BMW", daten)
            .OfType<InfluxPercentageRecord>().Single()
            .Value_Percent.Should().Be(0m, "an empty battery is a fact, an absent value is not");
    }

    // --------------------------------------------------------------- Tags

    [Fact]
    public void Das_Fahrzeug_kommt_aus_dem_Topic_und_nicht_aus_dem_Payload()
    {
        var daten = new CarStatusData { Nickname = "Dickes B", Brand = "BMW", Name = "i4", Battery = 50 };

        var record = Fahrzeugdaten.NachInfluxRecords("Mini", daten).Single();

        record.Device.Should().Be("Mini");
        record.Location.Should().Be("-", "a vehicle moves — it has no location");
        record.SensorType.Should().Be("Fahrzeug");
        record.Category.Should().Be(MeasurementCategory.Fahrzeug);
        record.MeasurementId.Should().Be("Fahrzeug_Mini_Ladestand");
    }

    // ------------------------------------------------- Tabellenzuordnung

    [Fact]
    public void Jeder_Wert_landet_in_der_Tabelle_seiner_Groesse()
    {
        var daten = new CarStatusData
        {
            Battery = 62,
            ChargingTarget = 80,
            RemainingRange = 210,
            PredictedRange = 280,
            Mileage = 41234.5,
            ChargingPower = 7400,
            MaxEnergy = 80.7,
            ChargerConnected = true,
            ChargingStatus = "CHARGINGACTIVE",
            Moving = false,
            Position = new SharedContracts.GeoPosition { Latitude = 48.4127, Longitude = 9.8753 },
        };

        var records = Fahrzeugdaten.NachInfluxRecords("BMW", daten);

        records.Should().HaveCount(11);
        records.OfType<InfluxPercentageRecord>().Select(r => r.Measurement)
            .Should().BeEquivalentTo("Ladestand", "Ladeziel");
        records.OfType<InfluxDistanceRecord>().Select(r => r.Measurement)
            .Should().BeEquivalentTo("Reichweite", "ReichweiteNachLadung", "Kilometerstand");
        records.OfType<InfluxPowerRecord>().Single().Measurement.Should().Be("Ladeleistung");
        records.OfType<InfluxEnergyRecord>().Single().Measurement.Should().Be("Batteriekapazitaet");
        records.OfType<InfluxStatusRecord>().Select(r => r.Measurement)
            .Should().BeEquivalentTo("SteckerVerbunden", "Ladestatus", "Faehrt");
        records.OfType<InfluxPositionRecord>().Single().Measurement.Should().Be("Position");
    }

    [Fact]
    public void Ein_Kilometerstand_jenseits_von_32767_ueberlebt()
    {
        var daten = new CarStatusData { Mileage = 145_600 };

        Fahrzeugdaten.NachInfluxRecords("VW", daten)
            .OfType<InfluxDistanceRecord>().Single()
            .Value_Km.Should().Be(145_600m,
                "this is why the odometer is not a counter — counter and status are Int16");
    }

    [Fact]
    public void Die_Batteriekapazitaet_wird_als_Zustand_und_nicht_als_Zaehler_geschrieben()
    {
        var daten = new CarStatusData { MaxEnergy = 80.7 };

        var record = Fahrzeugdaten.NachInfluxRecords("BMW", daten).OfType<InfluxEnergyRecord>().Single();

        record.Value_Cumulated_KWh.Should().Be(80.7m);
        record.Value_Delta_KWh.Should().Be(0m, "no energy flowed — MAX-MIN over any period must stay 0");
    }

    [Fact]
    public void Der_Steckerzustand_wird_als_eins_oder_null_geschrieben()
    {
        Fahrzeugdaten.NachInfluxRecords("BMW", new CarStatusData { ChargerConnected = true })
            .OfType<InfluxStatusRecord>().Single().Value_Status.Should().Be(1m);
        Fahrzeugdaten.NachInfluxRecords("BMW", new CarStatusData { ChargerConnected = false })
            .OfType<InfluxStatusRecord>().Single().Value_Status.Should().Be(0m);
    }

    // ----------------------------------------------------------- Position

    [Fact]
    public void Eine_Position_wird_als_ein_Punkt_mit_zwei_Feldern_geschrieben()
    {
        var daten = new CarStatusData
        {
            Position = new SharedContracts.GeoPosition { Latitude = 48.412758, Longitude = 9.875185 },
        };

        var record = Fahrzeugdaten.NachInfluxRecords("BMW", daten).OfType<InfluxPositionRecord>().Single();

        record.Value_Latitude.Should().Be(48.412758);
        record.Value_Longitude.Should().Be(9.875185);
    }

    [Fact]
    public void Die_Position_null_null_wird_verworfen()
    {
        var daten = new CarStatusData { Position = new SharedContracts.GeoPosition() };

        Fahrzeugdaten.NachInfluxRecords("VW", daten).Should().BeEmpty(
            "an empty position object deserialises into the Gulf of Guinea, not into a location");
    }

    [Fact]
    public void Ein_Payload_ohne_Position_schreibt_keine()
    {
        // The VW omits the key entirely — the EU Data Act export carries no GPS data at all.
        var payload = """{"brand":"VW","name":"ID.4","battery":55,"lastUpdate":"2026-09-20T10:00:00Z"}""";
        var daten = JsonSerializer.Deserialize<CarStatusData>(payload, JsonOptions)!;

        Fahrzeugdaten.NachInfluxRecords("VW", daten)
            .OfType<InfluxPositionRecord>().Should().BeEmpty();
    }

    // -------------------------------------------- Echte Payloads der drei

    [Fact]
    public void Der_Payload_des_BMW_Connectors_wird_vollstaendig_verarbeitet()
    {
        // Shaped like BMWConnector VehicleState.ToJson(), with the fields backlog item 10 added.
        var payload = """
        {
          "Zeitpunkt": "2026-09-20T12:00:00.0000000+00:00",
          "brand": "BMW", "name": "BMW",
          "battery": 62, "chargingStatus": "CHARGINGACTIVE", "hvChargingStatus": "HV_CHARGING",
          "chargingTarget": 80, "chargerConnected": true,
          "remainingRange": 210.0, "predictedRange": 280.0, "mileage": 41234.5,
          "position": { "latitude": 48.412758, "longitude": 9.875185 },
          "moving": false, "chargingPower": 7400.0, "maxEnergy": 80.7,
          "chargingMode": "NORMAL_PROGNOSE_BASED", "plugEventId": 17,
          "avgConsumption": 18.4, "acVoltage": 230.0, "acAmpere": 16.0,
          "lastUpdate": "2026-08-16T09:30:00.0000000Z"
        }
        """;
        var daten = JsonSerializer.Deserialize<CarStatusData>(payload, JsonOptions)!;

        Fahrzeugdaten.Messzeit(daten, Jetzt).Should().Be(
            new DateTimeOffset(2026, 8, 16, 9, 30, 0, TimeSpan.Zero),
            "35 days old and nothing is broken — CarData pushes only on vehicle events");

        var records = Fahrzeugdaten.NachInfluxRecords("BMW", daten);

        records.Should().HaveCount(11);
        records.Should().OnlyContain(r => r.Device == "BMW" && r.Location == "-");
    }

    [Fact]
    public void Der_Payload_des_VW_Connectors_liefert_was_der_EU_Data_Act_hergibt()
    {
        var payload = """
        {
          "Zeitpunkt": "2026-09-20T11:58:00+02:00",
          "nickname": "ID.4", "brand": "VW", "name": "ID.4",
          "battery": 55, "remainingRange": 260.0, "mileage": 28910.0,
          "chargerConnected": false, "chargingStatus": "off", "chargingTarget": 80,
          "state": "parked",
          "lastUpdate": "2026-09-20T11:45:00+02:00"
        }
        """;
        var daten = JsonSerializer.Deserialize<CarStatusData>(payload, JsonOptions)!;

        var records = Fahrzeugdaten.NachInfluxRecords("VW", daten);

        records.Select(r => r.Measurement).Should().BeEquivalentTo(
            "Ladestand", "Ladeziel", "Reichweite", "Kilometerstand", "SteckerVerbunden", "Ladestatus");
        records.OfType<InfluxStatusRecord>().Single(r => r.Measurement == "Ladestatus")
            .Value_Status.Should().Be(0m);
    }

    [Fact]
    public void Der_Mini_meldet_weniger_und_das_ist_kein_Fehler()
    {
        // The Mini reports its state of charge in real time but no odometer and no capacity.
        var daten = new CarStatusData
        {
            Battery = 71,
            ChargingStatus = "NOCHARGING",
            ChargerConnected = false,
            RemainingRange = 180,
        };

        Fahrzeugdaten.NachInfluxRecords("Mini", daten).Select(r => r.Measurement)
            .Should().BeEquivalentTo("Ladestand", "Reichweite", "SteckerVerbunden", "Ladestatus");
    }
}
