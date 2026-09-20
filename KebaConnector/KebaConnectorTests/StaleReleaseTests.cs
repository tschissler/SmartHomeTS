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
///
/// The age therefore comes from the Zeitpunkt in the command payload — when the controller
/// decided the setpoint — and never from the time the message arrived. That is the durable
/// fix; judging a delivery by its retain flag only told replayed from live, never how old.
/// </summary>
public class StaleReleaseTests
{
    private static readonly TimeSpan StaleReleaseAfter = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SetpointFreshDuration = TimeSpan.FromMinutes(5);
    private const int OffCommand = 0;
    private const int SomeCurrent = 10000;

    /// <summary>
    /// The connector restarts while the ChargingController is dead and the broker replays its
    /// last command. The release is due StaleReleaseAfter after the controller *sent* that
    /// command, no matter when the replay happens to be delivered — at once, or minutes later
    /// if the broker is unreachable at first and the reconnect loop has to retry.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Releases_StaleReleaseAfterTheCommandWasSent_NotAfterItArrived(int minutesUntilReplay)
    {
        var (connector, clock, writes) = CreateConnector();
        // The controller sent this 8 minutes ago and died; the connector started just now.
        var sentAt = clock.Now - TimeSpan.FromMinutes(8);

        clock.Advance(TimeSpan.FromMinutes(minutesUntilReplay));
        connector.ReceiveCommand(OffCommand, sentAt);

        // Due 10 minutes after it was sent, i.e. 2 minutes after the connector started
        clock.AdvanceTo(sentAt + StaleReleaseAfter + TimeSpan.FromSeconds(1));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));

        writes.Should().Contain(KebaDeviceConnector.ReleaseCurrent,
            "a replay is no proof that the controller is alive, so the release must still fire");
    }

    [Fact]
    public void Releases_EvenWhenTheCommandIsReplayedAgain()
    {
        // Every MQTT reconnect re-subscribes and therefore gets the retained command
        // delivered again. Judged by arrival time each replay restarted the freshness clock,
        // so a reconnect within 10 minutes pushed the release out indefinitely. The Zeitpunkt
        // does not move, so repeating the delivery changes nothing.
        var (connector, clock, writes) = CreateConnector();
        var sentAt = clock.Now;
        connector.ReceiveCommand(OffCommand, sentAt);

        clock.Advance(TimeSpan.FromMinutes(6));
        connector.ReceiveCommand(OffCommand, sentAt);

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
        connector.ReceiveCommand(OffCommand, clock.Now);

        clock.Advance(TimeSpan.FromMinutes(5));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));

        writes.Should().NotContain(KebaDeviceConnector.ReleaseCurrent);
    }

    [Fact]
    public void AppliesTheSetpoint_FromAReplayedCommand_WhileItIsStillFresh()
    {
        // A recent value is still the best setpoint known right after a restart:
        // it must reach the box, it just must not reset the age.
        var (connector, clock, writes) = CreateConnector();
        connector.ReceiveCommand(SomeCurrent, clock.Now - TimeSpan.FromSeconds(30));

        clock.Advance(TimeSpan.FromMinutes(1));
        connector.Enforce(BoxState(targetCurrency: 6000, chargingEnabled: true));

        writes.Should().Contain(SomeCurrent);
    }

    [Fact]
    public void DoesNotApplyTheSetpoint_FromACommandPastItsFreshness()
    {
        // Between SetpointFreshDuration and StaleReleaseAfter the connector neither enforces
        // the setpoint nor releases the box: writing a setpoint that is already too old to be
        // reconciled would switch the box to a value nobody is controlling any more.
        var (connector, clock, writes) = CreateConnector();

        connector.ReceiveCommand(OffCommand, clock.Now - SetpointFreshDuration - TimeSpan.FromMinutes(1));
        clock.Advance(TimeSpan.FromSeconds(5));
        connector.Enforce(BoxState(targetCurrency: SomeCurrent, chargingEnabled: true));

        writes.Should().BeEmpty("a setpoint past its freshness is kept, but not applied");
    }

    [Fact]
    public void KeepsTheBoxReleased_WhenAStaleCommandArrivesAfterTheRelease()
    {
        // A reconnect after the release replays the stale command once more. Applying it
        // would switch the box off again with no controller left to switch it back on.
        var (connector, clock, writes) = CreateConnector();
        var sentAt = clock.Now;
        connector.ReceiveCommand(OffCommand, sentAt);

        clock.Advance(StaleReleaseAfter + TimeSpan.FromMinutes(1));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));
        writes.Should().Contain(KebaDeviceConnector.ReleaseCurrent);
        writes.Clear();

        connector.ReceiveCommand(OffCommand, sentAt);
        clock.Advance(TimeSpan.FromMinutes(1));
        connector.Enforce(BoxState(targetCurrency: KebaDeviceConnector.ReleaseCurrent, chargingEnabled: true));

        writes.Should().BeEmpty("the stale setpoint must not undo the release");
    }

    [Fact]
    public void Releases_WhenACommandCarriesNoZeitpunkt()
    {
        // A payload without a Zeitpunkt deserialises to default(DateTimeOffset). Program.cs
        // rejects such a command outright; should one ever reach here, it has to count as
        // ancient rather than as fresh — erring towards the release keeps the car charging.
        var (connector, clock, writes) = CreateConnector();

        connector.ReceiveCommand(OffCommand, default);
        clock.Advance(TimeSpan.FromSeconds(5));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));

        writes.Should().Contain(KebaDeviceConnector.ReleaseCurrent);
    }

    [Fact]
    public void Releases_EvenWhenTheCommandIsDatedInTheFuture()
    {
        // The two services keep their own clocks. A setpoint dated ahead of the connector
        // would have an age that never grows and could therefore never become stale — the
        // box would stay on it forever. Clamping the timestamp keeps the release reachable.
        var (connector, clock, writes) = CreateConnector();

        connector.ReceiveCommand(OffCommand, clock.Now + TimeSpan.FromHours(1));

        clock.Advance(StaleReleaseAfter + TimeSpan.FromMinutes(1));
        connector.Enforce(BoxState(targetCurrency: OffCommand, chargingEnabled: false));

        writes.Should().Contain(KebaDeviceConnector.ReleaseCurrent,
            "a setpoint from a clock running ahead must not become immortal");
    }

    private static (TestableKebaDeviceConnector connector, TestClock clock, List<int> writes) CreateConnector()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var connector = new TestableKebaDeviceConnector(clock);
        return (connector, clock, connector.Writes);
    }

    /// <summary>Device data with a vehicle plugged in — the only state EnforceDesiredState writes in.</summary>
    internal static KebaData BoxState(int targetCurrency, bool chargingEnabled,
        PlugStatus plugStatus = PlugStatus.CablePluggedInChargingStationAndVehicleAndLocked) => new(
        PlugStatus: plugStatus,
        DeviceState: chargingEnabled ? 3 : 1,
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

    internal sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public DateTimeOffset Now => now;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;

        public void AdvanceTo(DateTimeOffset moment) => now = moment;
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

        public void ReceiveCommand(int current, DateTimeOffset sentAt)
            => UpdateDeviceDesiredCurrent(current, sentAt).GetAwaiter().GetResult();

        public void Enforce(KebaData data)
            => EnforceDesiredState(data, "Test").GetAwaiter().GetResult();
    }
}
