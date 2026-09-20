using System.Net;
using FluentAssertions;
using KebaConnector;
using SharedContracts;

namespace KebaConnectorTests;

/// <summary>
/// The status topic carries SitzungsId and SitzungsBeginn so the ChargingController can tell
/// where one charging session ends and the next begins — it owns the Ladesitzung topic, the
/// connector only reports what the box knows.
///
/// The recorded reports of the box show "timeQ": 0, "not synced time": its clock may be
/// arbitrarily wrong. The session start therefore comes from the plug-in edge this connector
/// observes itself, and falls back to the box clock only for a session that was already
/// running when the connector started — a case the payload marks as such.
/// </summary>
public class SessionTrackingTests
{
    private const string Wallbox = "Garage";
    private const int SessionId = 42;

    [Fact]
    public void UsesTheObservedPlugInEdge_AsTheSessionStart()
    {
        var (connector, clock) = CreateConnector();

        connector.Track(Unplugged());
        clock.Advance(TimeSpan.FromSeconds(5));
        var plugInAt = clock.Now;
        connector.Track(Plugged());

        // The box opens the session a moment after the plug is locked, so the id shows up a
        // cycle later than the edge — the edge is remembered until it does.
        clock.Advance(TimeSpan.FromSeconds(5));
        connector.NextSession = RunningSession(SessionId, startedInBox: clock.Now.AddHours(-9));
        connector.Track(Plugged());

        connector.RunningSessionId.Should().Be(SessionId);
        connector.SessionStart.Should().Be(plugInAt, "the observed edge beats the box clock");
        connector.SessionStartFromBoxClock.Should().BeFalse();
    }

    [Fact]
    public void FallsBackToTheBoxClock_ForASessionThatWasAlreadyRunning()
    {
        // The connector restarts while a car is charging: there is no edge to observe, so the
        // only source left is the clock of the box — flagged, because it may be wrong.
        var (connector, clock) = CreateConnector();
        var startedInBox = clock.Now - TimeSpan.FromHours(2);

        connector.NextSession = RunningSession(SessionId, startedInBox);
        connector.Track(Plugged());

        connector.RunningSessionId.Should().Be(SessionId);
        connector.SessionStart.Should().Be(startedInBox);
        connector.SessionStartFromBoxClock.Should().BeTrue(
            "a consumer calculating a duration has to know the start came from an unsynchronised clock");
    }

    [Fact]
    public void ClearsTheSession_WhenItEnds()
    {
        var (connector, clock) = CreateConnector();
        connector.NextSession = RunningSession(SessionId, clock.Now);
        connector.Track(Plugged());
        connector.RunningSessionId.Should().Be(SessionId);

        connector.NextSession = null;
        connector.Track(Unplugged());

        connector.RunningSessionId.Should().BeNull();
        connector.SessionStart.Should().BeNull();
        connector.SessionStartFromBoxClock.Should().BeFalse();
    }

    [Fact]
    public void KeepsTheStart_WhileTheSameSessionRuns()
    {
        // The start must not creep forward with every read cycle: the session duration in the
        // charging log is calculated from it.
        var (connector, clock) = CreateConnector();
        connector.Track(Unplugged());
        var plugInAt = clock.Now;
        connector.Track(Plugged());
        connector.NextSession = RunningSession(SessionId, clock.Now);
        connector.Track(Plugged());

        for (var cycle = 0; cycle < 5; cycle++)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            connector.Track(Plugged());
        }

        connector.SessionStart.Should().Be(plugInAt);
    }

    [Fact]
    public void DoesNotReuseAPlugInEdge_ForASecondSessionOnTheSamePlug()
    {
        // A session can end and a new one begin without unplugging — the box does that on
        // deauthorisation. There is no second edge to observe, so the new session falls back
        // to the box clock; it must not silently inherit the edge of the previous session and
        // report a start hours before it actually began.
        var (connector, clock) = CreateConnector();
        connector.Track(Unplugged());
        connector.Track(Plugged());
        connector.NextSession = RunningSession(SessionId, clock.Now);
        connector.Track(Plugged());

        connector.NextSession = null;
        connector.Track(Plugged());

        clock.Advance(TimeSpan.FromHours(3));
        var startedInBox = clock.Now;
        connector.NextSession = RunningSession(SessionId + 1, startedInBox);
        connector.Track(Plugged());

        connector.SessionStart.Should().Be(startedInBox);
        connector.SessionStartFromBoxClock.Should().BeTrue();
    }

    [Fact]
    public void ObservesAFreshEdge_AfterUnpluggingAndPluggingInAgain()
    {
        // The counter-check to the test above: unplug, plug in again, and the newly observed
        // edge is the better source — the box clock stays the fallback, not the default.
        var (connector, clock) = CreateConnector();
        connector.Track(Unplugged());
        connector.Track(Plugged());
        connector.NextSession = RunningSession(SessionId, clock.Now);
        connector.Track(Plugged());

        connector.NextSession = null;
        connector.Track(Unplugged());

        clock.Advance(TimeSpan.FromHours(3));
        var plugInAt = clock.Now;
        connector.Track(Plugged());
        connector.NextSession = RunningSession(SessionId + 1, clock.Now.AddHours(-9));
        connector.Track(Plugged());

        connector.SessionStart.Should().Be(plugInAt);
        connector.SessionStartFromBoxClock.Should().BeFalse();
    }

    [Fact]
    public void IgnoresASessionTheBoxHasAlreadyEnded()
    {
        // Report 100 keeps the last session after it ended; only one without an end time is
        // running. Treating a finished one as running would start a session that never ends.
        var (connector, clock) = CreateConnector();

        connector.NextSession = new ChargingSession(
            SessionId: SessionId,
            StartTime: clock.Now - TimeSpan.FromHours(1),
            EndTime: clock.Now - TimeSpan.FromMinutes(5),
            TatalEnergyAtStart: 0,
            EnergyOfChargingSession: 0,
            WallboxName: Wallbox,
            ChargedCar: "");
        connector.Track(Plugged());

        connector.RunningSessionId.Should().BeNull();
    }

    [Fact]
    public void TreatsAnUnlockedCableAsNotConnected()
    {
        // Plug state 5 is cable in the vehicle but not locked — charging is impossible until
        // state 7, so it must not count as the plug-in edge of a session.
        var (connector, clock) = CreateConnector();
        connector.Track(Unplugged());
        connector.Track(StaleReleaseTests.BoxState(0, false,
            PlugStatus.CablePluggedInChargingStationAndVehicleButNotLocked));

        var startedInBox = clock.Now - TimeSpan.FromMinutes(30);
        connector.NextSession = RunningSession(SessionId, startedInBox);
        connector.Track(Plugged());

        // An edge was observed at state 5 already, so the start is that edge, not the box clock
        connector.SessionStart.Should().NotBeNull();
        connector.SessionStartFromBoxClock.Should().BeFalse();
    }

    private static ChargingSession RunningSession(int sessionId, DateTimeOffset startedInBox) => new(
        SessionId: sessionId,
        StartTime: startedInBox,
        EndTime: null,
        TatalEnergyAtStart: 0,
        EnergyOfChargingSession: 0,
        WallboxName: Wallbox,
        ChargedCar: "");

    private static KebaData Plugged() => StaleReleaseTests.BoxState(6000, true);

    private static KebaData Unplugged() => StaleReleaseTests.BoxState(6000, false,
        PlugStatus.CableNotPluggedIn);

    private static (TestableSessionConnector connector, StaleReleaseTests.TestClock clock) CreateConnector()
    {
        var clock = new StaleReleaseTests.TestClock(new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.Zero));
        return (new TestableSessionConnector(clock), clock);
    }

    /// <summary>Replaces the UDP read of report 100 with whatever the test wants to see.</summary>
    private sealed class TestableSessionConnector(TimeProvider time)
        : KebaDeviceConnector(IPAddress.Loopback, 7090, timeProvider: time)
    {
        public ChargingSession? NextSession { get; set; }

        protected override bool WritesEnabled() => false;

        protected override void WriteChargingCurrentToDevice(int current) { }

        protected override ChargingSession? ReadRunningSession(string wallbox)
        {
            if (NextSession is null || NextSession.EndTime is not null || NextSession.SessionId == 0)
                return null;
            return NextSession;
        }

        public void Track(KebaData data) => TrackSession(data, Wallbox);
    }
}
