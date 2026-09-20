using System.Net;
using FluentAssertions;
using KebaConnector;

namespace KebaConnectorTests;

/// <summary>
/// The wallbox has an emergency release: when no charging command has arrived for
/// StaleReleaseAfter (10 minutes), the box is opened to full current so the car keeps
/// charging even though the control loop is down. The ChargingController publishes its
/// command retained, and the broker replays a retained message on every subscribe — on
/// connector startup and again on every MQTT reconnect. If such a replay counted as a
/// fresh command, the freshness clock would restart and the release would never fire,
/// leaving the box stuck on an arbitrarily old setpoint — permanently switched off if
/// that setpoint was 0 mA.
/// </summary>
public class StaleReleaseTests
{
    private static readonly TimeSpan StaleReleaseAfter = TimeSpan.FromMinutes(10);
    private const int OffCommand = 0;
    private const int SomeCurrent = 10000;

    /// <summary>
    /// The connector restarts while the ChargingController is dead, the broker replays the
    /// last command, and nothing else ever arrives. The release is due StaleReleaseAfter
    /// after the connector started, no matter when the replay happened to be delivered —
    /// the broker may hand it over at once, or minutes later if it is unreachable at first
    /// and the reconnect loop has to retry.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Releases_AfterStaleReleaseAfter_WhenOnlyARetainedCommandArrived(int minutesUntilReplay)
    {
        var (connector, clock, writes) = CreateConnector();

        clock.Advance(TimeSpan.FromMinutes(minutesUntilReplay));
        connector.ReceiveCommand(OffCommand, retained: true);

        clock.Advance(StaleReleaseAfter + TimeSpan.FromMinutes(1) - TimeSpan.FromMinutes(minutesUntilReplay));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));

        writes.Should().Contain(KebaDeviceConnector.ReleaseCurrent,
            "a retained replay is no proof that the controller is alive, so the release must still fire");
    }

    [Fact]
    public void Releases_EvenWhenTheRetainedCommandIsReplayedAgain()
    {
        // Every MQTT reconnect re-subscribes and therefore gets the retained command
        // delivered again. Before the fix each replay restarted the freshness clock, so a
        // reconnect that happened within 10 minutes pushed the release out indefinitely.
        var (connector, clock, writes) = CreateConnector();
        connector.ReceiveCommand(OffCommand, retained: true);

        clock.Advance(TimeSpan.FromMinutes(6));
        connector.ReceiveCommand(OffCommand, retained: true);

        clock.Advance(TimeSpan.FromMinutes(5));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));

        writes.Should().Contain(KebaDeviceConnector.ReleaseCurrent,
            "the age is counted from the last real command, not from the last delivery");
    }

    [Fact]
    public void DoesNotRelease_WhileTheControllerKeepsSendingRealCommands()
    {
        // Counter-check: a command the controller actually sent must reset the clock,
        // otherwise the fix would release a box that is under active control.
        var (connector, clock, writes) = CreateConnector();

        clock.Advance(TimeSpan.FromMinutes(6));
        connector.ReceiveCommand(OffCommand, retained: false);

        clock.Advance(TimeSpan.FromMinutes(5));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));

        writes.Should().NotContain(KebaDeviceConnector.ReleaseCurrent);
    }

    [Fact]
    public void AppliesTheSetpoint_FromARetainedCommand_WhileItIsStillFresh()
    {
        // The retained value is still the best setpoint known right after a restart:
        // it must reach the box, it just must not reset the age.
        var (connector, clock, writes) = CreateConnector();
        connector.ReceiveCommand(SomeCurrent, retained: true);

        clock.Advance(TimeSpan.FromMinutes(1));
        connector.Enforce(BoxState(targetCurrency: 6000, chargingEnabled: true));

        writes.Should().Contain(SomeCurrent);
    }

    [Fact]
    public void KeepsTheBoxReleased_WhenARetainedCommandArrivesAfterTheRelease()
    {
        // A reconnect after the release replays the stale command once more. Applying it
        // would switch the box off again with no controller left to switch it back on.
        var (connector, clock, writes) = CreateConnector();
        connector.ReceiveCommand(OffCommand, retained: true);

        clock.Advance(StaleReleaseAfter + TimeSpan.FromMinutes(1));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));
        writes.Should().Contain(KebaDeviceConnector.ReleaseCurrent);
        writes.Clear();

        connector.ReceiveCommand(OffCommand, retained: true);
        clock.Advance(TimeSpan.FromMinutes(1));
        connector.Enforce(BoxState(targetCurrency: KebaDeviceConnector.ReleaseCurrent, chargingEnabled: true));

        writes.Should().BeEmpty("the stale setpoint must not undo the release");
    }

    private static (TestableKebaDeviceConnector connector, TestClock clock, List<int> writes) CreateConnector()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var connector = new TestableKebaDeviceConnector(clock);
        return (connector, clock, connector.Writes);
    }

    /// <summary>Device data with a vehicle plugged in — the only state EnforceDesiredState writes in.</summary>
    private static KebaData BoxState(int targetCurrency, bool chargingEnabled) => new(
        PlugStatus: PlugStatus.CablePluggedInChargingStationAndVehicleAndLocked,
        ChargingEnabled: chargingEnabled,
        DeviceEnabled: true,
        MaxCurrencyOfferedByChargingStation: 16000,
        MaxCurrencyOfferedByChargingStationPercent: 1000,
        MaxCurrencyPossibleByChargingStation: 16000,
        TargetCurrency: targetCurrency,
        TargetEnergy: 0,
        SerialNumber: "22588720",
        VoltagePhase1: 230, VoltagePhase2: 230, VoltagePhase3: 230,
        CurrencyPhase1: 0, CurrencyPhase2: 0, CurrencyPhase3: 0,
        CurrentChargingPower: 0,
        EnergyCurrentChargingSession: 0,
        EnergyTotal: 0);

    private sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    /// <summary>
    /// Replaces the two things that would talk to a real wallbox: the UDP write and the
    /// KEBA_WRITE_TO_DEVICE environment flag that guards it.
    /// </summary>
    private sealed class TestableKebaDeviceConnector(TimeProvider time)
        : KebaDeviceConnector(IPAddress.Loopback, 7090, timeProvider: time)
    {
        public List<int> Writes { get; } = [];

        protected override bool WritesEnabled() => true;

        protected override void WriteChargingCurrentToDevice(int current) => Writes.Add(current);

        public void ReceiveCommand(int current, bool retained)
            => UpdateDeviceDesiredCurrent(current, retained).GetAwaiter().GetResult();

        public void Enforce(KebaData data)
            => EnforceDesiredState(data, "Test").GetAwaiter().GetResult();
    }
}
