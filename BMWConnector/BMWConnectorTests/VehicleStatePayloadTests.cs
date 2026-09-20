using System.Text.Json;
using BMWConnector.Models;
using FluentAssertions;
using SharedContracts;

namespace BMWConnectorTests;

/// <summary>
/// The payload of the connector read back through the contract its consumers use. The two ends
/// used to be connected by nothing but hope: <see cref="VehicleState.ToJson"/> writes an
/// anonymous object, <see cref="CarStatusData"/> reads it, and a field spelled differently on
/// one side simply vanished — System.Text.Json drops what it does not know, without a word.
/// That is how eight fields were being thrown away (backlog item 10).
/// </summary>
public class VehicleStatePayloadTests
{
    private static BmwDataPoint Point(string valueJson) =>
        JsonSerializer.Deserialize<BmwDataPoint>(
            $$"""{"timestamp":"2026-09-20T07:58:00Z","value":{{valueJson}}}""")!;

    /// <summary>Applies one datapoint and hands back what a consumer would see.</summary>
    private static CarStatusData RoundTrip(params (string Field, string Value)[] datenpunkte)
    {
        var state = new VehicleState("BMW");
        foreach (var (field, value) in datenpunkte)
            state.Apply(field, Point(value)).Should().BeTrue($"'{field}' must be a mapped field");

        return JsonSerializer.Deserialize<CarStatusData>(state.ToJson())!;
    }

    /// <summary>
    /// Every field the connector can produce has to arrive on the other side. Listed one by one
    /// rather than reflected over, so that adding a field to the connector without adding it to
    /// the contract is a red test and not a silent loss.
    /// </summary>
    [Fact]
    public void Every_published_field_survives_the_contract()
    {
        var gelesen = RoundTrip(
            ("vehicle.drivetrain.batteryManagement.header", "83"),
            ("vehicle.drivetrain.batteryManagement.maxEnergy", "80.7"),
            ("vehicle.drivetrain.electricEngine.charging.status", "\"CHARGINGACTIVE\""),
            ("vehicle.drivetrain.electricEngine.charging.hvStatus", "\"HV_CHARGING\""),
            ("vehicle.powertrain.electric.battery.stateOfCharge.target", "90"),
            ("vehicle.body.chargingPort.status", "\"CONNECTED\""),
            ("vehicle.body.chargingPort.plugEventId", "17"),
            ("vehicle.drivetrain.electricEngine.kombiRemainingElectricRange", "312"),
            ("vehicle.drivetrain.electricEngine.remainingElectricRange", "388"),
            ("vehicle.vehicle.travelledDistance", "24512"),
            ("vehicle.drivetrain.avgElectricRangeConsumption", "18.4"),
            ("vehicle.cabin.infotainment.navigation.currentLocation.latitude", "48.1"),
            ("vehicle.cabin.infotainment.navigation.currentLocation.longitude", "11.6"),
            ("vehicle.isMoving", "false"),
            ("vehicle.powertrain.electric.battery.charging.power", "10900"),
            ("vehicle.drivetrain.electricEngine.charging.chargingMode", "\"NORMAL_PROGNOSE_BASED\""),
            ("vehicle.drivetrain.electricEngine.charging.acVoltage", "230"),
            ("vehicle.drivetrain.electricEngine.charging.acAmpere", "16"));

        gelesen.Battery.Should().Be(83);
        gelesen.MaxEnergy.Should().Be(80.7);
        gelesen.ChargingStatus.Should().Be("CHARGINGACTIVE");
        gelesen.HvChargingStatus.Should().Be("HV_CHARGING");
        gelesen.ChargingTarget.Should().Be(90);
        gelesen.ChargerConnected.Should().BeTrue();
        gelesen.PlugEventId.Should().Be(17);
        gelesen.RemainingRange.Should().Be(312);
        gelesen.PredictedRange.Should().Be(388);
        gelesen.Mileage.Should().Be(24512);
        gelesen.AvgConsumption.Should().Be(18.4);
        gelesen.Position!.Latitude.Should().Be(48.1);
        gelesen.Position!.Longitude.Should().Be(11.6);
        gelesen.Moving.Should().BeFalse();
        gelesen.ChargingPower.Should().Be(10900);
        gelesen.ChargingMode.Should().Be("NORMAL_PROGNOSE_BASED");
        gelesen.AcVoltage.Should().Be(230);
        gelesen.AcAmpere.Should().Be(16);
        gelesen.Brand.Should().Be("BMW");
        // Unchanged all the way through, UTC included: the BMW stamps its datapoints with "Z",
        // and the round trip must not quietly shift them into the local zone on the way.
        gelesen.LastUpdate.Should().Be(new DateTime(2026, 9, 20, 7, 58, 0, DateTimeKind.Utc));
        gelesen.LastUpdate!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    /// <summary>
    /// Mandatory field of the topic convention. The message is retained, so without it a
    /// consumer reconnecting cannot tell a live connector from one that died weeks ago.
    /// </summary>
    [Fact]
    public void The_payload_carries_a_publication_timestamp()
    {
        var vorher = DateTimeOffset.UtcNow.AddSeconds(-1);

        var gelesen = RoundTrip(("vehicle.drivetrain.batteryManagement.header", "83"));

        gelesen.Zeitpunkt.Should().NotBeNull();
        gelesen.Zeitpunkt!.Value.Should().BeOnOrAfter(vorher);
    }

    /// <summary>
    /// A value the vehicle never sent must stay absent instead of arriving as 0. The connector
    /// omits nulls, the contract reads them back as null, and the interface shows "—" — the
    /// whole chain that keeps "0 km" from being printed as if the vehicle had said it.
    /// </summary>
    [Fact]
    public void A_field_the_vehicle_never_sent_stays_null_instead_of_becoming_zero()
    {
        var gelesen = RoundTrip(("vehicle.drivetrain.batteryManagement.header", "83"));

        gelesen.Mileage.Should().BeNull();
        gelesen.RemainingRange.Should().BeNull();
        gelesen.MaxEnergy.Should().BeNull();
        gelesen.ChargingPower.Should().BeNull();
        gelesen.Position.Should().BeNull();
        gelesen.Moving.Should().BeNull();
        gelesen.ChargerConnected.Should().BeNull();
    }
}
