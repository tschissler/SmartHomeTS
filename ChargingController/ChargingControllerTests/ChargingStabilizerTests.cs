using ChargingController;
using FluentAssertions;
using SharedContracts;

namespace ChargingControllerTests
{
    public class ChargingStabilizerTests
    {
        private static readonly DateTimeOffset T0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.FromHours(2));
        private const int MinimumCurrentmA = ChargingDecisionsMaker.MinimumChargingCurrentmA;

        private readonly ChargingStabilizerOptions options = new();
        private readonly ChargingStabilizer stabilizer;

        public ChargingStabilizerTests()
        {
            stabilizer = new ChargingStabilizer(options);
        }

        [Fact]
        public void ChargingStartsOnlyAfterTheSurplusWasSufficientForTheStartDelay()
        {
            Run(T0, MinimumCurrentmA).Should().Be(0, "the surplus was seen for the first time");
            Run(T0.AddSeconds(25), MinimumCurrentmA).Should().Be(0, "the start delay of 30 s has not passed");
            Run(T0.AddSeconds(30), MinimumCurrentmA).Should().Be(MinimumCurrentmA, "the start delay has passed");
        }

        [Fact]
        public void ShortConsumptionPeakDoesNotInterruptCharging()
        {
            StartCharging();

            // Peak: the ideal decision would switch the wallbox off for 60 s
            Run(T0.AddSeconds(60), 0).Should().Be(MinimumCurrentmA, "a peak must not open the contactor");
            Run(T0.AddSeconds(100), 0).Should().Be(MinimumCurrentmA, "the stop delay of 90 s has not passed");
            Run(T0.AddSeconds(120), 8000).Should().Be(8000, "the surplus is back, so the setpoint follows again");
        }

        [Fact]
        public void PersistentShortfallStopsChargingOnlyAfterTheMinimumChargingDuration()
        {
            StartCharging();

            Run(T0.AddSeconds(60), 0).Should().Be(MinimumCurrentmA);
            // Stop delay (90 s) has passed, but the minimum charging duration (5 min) has not
            Run(T0.AddSeconds(200), 0).Should().Be(MinimumCurrentmA, "charging started 170 s ago");
            Run(T0.AddSeconds(340), 0).Should().Be(0, "charging ran for more than 5 minutes and the surplus is gone");
        }

        [Fact]
        public void ChargingDoesNotRestartBeforeTheMinimumPauseHasPassed()
        {
            StartCharging();
            Run(T0.AddSeconds(60), 0).Should().Be(MinimumCurrentmA);
            Run(T0.AddSeconds(340), 0).Should().Be(0, "charging stops here");

            Run(T0.AddSeconds(350), MinimumCurrentmA).Should().Be(0);
            Run(T0.AddSeconds(400), MinimumCurrentmA).Should().Be(0, "the minimum pause of 5 minutes has not passed");
            Run(T0.AddSeconds(650), MinimumCurrentmA).Should().Be(MinimumCurrentmA, "the minimum pause has passed");
        }

        [Fact]
        public void SmallSetpointChangesAreSwallowedByTheDeadband()
        {
            StartCharging();

            Run(T0.AddSeconds(60), MinimumCurrentmA + 300).Should().Be(MinimumCurrentmA, "300 mA is below the deadband");
            Run(T0.AddSeconds(90), MinimumCurrentmA + 400).Should().Be(MinimumCurrentmA, "400 mA is below the deadband");
            Run(T0.AddSeconds(120), MinimumCurrentmA + 600).Should().Be(MinimumCurrentmA + 600, "600 mA exceeds the deadband");
        }

        [Fact]
        public void SetpointChangesAreRateLimited()
        {
            StartCharging();

            Run(T0.AddSeconds(40), 9000).Should().Be(MinimumCurrentmA, "the last change was 10 s ago");
            Run(T0.AddSeconds(60), 9000).Should().Be(9000, "30 s have passed since the last change");
            Run(T0.AddSeconds(70), 12000).Should().Be(9000, "the last change was 10 s ago");
            Run(T0.AddSeconds(95), 12000).Should().Be(12000);
        }

        [Fact]
        public void UnpluggingTheCarStopsImmediatelyAndDoesNotArmThePause()
        {
            StartCharging();

            var situation = Situation();
            situation.InsideConnected = false;
            Stabilize(T0.AddSeconds(60), MinimumCurrentmA, situation).Should().Be(0, "no car is connected");

            // A new session may start after the normal start delay, the pause is for
            // control driven stops only
            Run(T0.AddSeconds(70), MinimumCurrentmA).Should().Be(0);
            Run(T0.AddSeconds(100), MinimumCurrentmA).Should().Be(MinimumCurrentmA);
        }

        [Fact]
        public void DisabledChargingStopsImmediately()
        {
            StartCharging();

            var settings = new ChargingSettings { ChargingLevel = 0, InsideChargingEnabled = true };
            Stabilize(T0.AddSeconds(60), MinimumCurrentmA, Situation(), settings).Should().Be(0);
        }

        [Fact]
        public void HighGridConsumptionThrottlesImmediatelyAndStopsIfItPersists()
        {
            Run(T0, 12000).Should().Be(0);
            Run(T0.AddSeconds(30), 12000).Should().Be(12000, "charging starts at full surplus");

            var overload = Situation(gridPower: 9000);
            Stabilize(T0.AddSeconds(40), 12000, overload).Should().Be(MinimumCurrentmA,
                "grid consumption above the limit throttles immediately, bypassing the rate limit");
            Stabilize(T0.AddSeconds(50), 12000, overload).Should().Be(MinimumCurrentmA,
                "the grid protection stop delay has not passed");
            Stabilize(T0.AddSeconds(75), 12000, overload).Should().Be(0,
                "grid consumption stayed above the limit, so charging stops despite the minimum duration");
        }

        [Fact]
        public void WithoutBatterySupportTheShortenedStopDelayApplies()
        {
            StartCharging();

            // The house battery dropped below its minimum level, so a gap now means grid consumption
            var situation = Situation();
            situation.BatterySupportedChargingActive = false;

            Stabilize(T0.AddSeconds(310), 0, situation).Should().Be(MinimumCurrentmA, "the shortfall was just detected");
            Stabilize(T0.AddSeconds(345), 0, situation).Should().Be(0, "30 s instead of 90 s without battery support");
        }

        [Fact]
        public void ARunningChargingSessionIsAdoptedInsteadOfBeingInterrupted()
        {
            // Service restarted while the car was charging with 10 A
            var situation = Situation(insideChargingPower: 6900);

            Stabilize(T0, 0, situation).Should().Be(MinimumCurrentmA,
                "the running session is adopted and throttled, not switched off");
            Stabilize(T0.AddSeconds(95), 0, situation).Should().Be(0,
                "after the stop delay it stops, without the minimum duration protecting a restart");
        }

        [Fact]
        public void ASessionAtTheMinimumCurrentIsRecognisedDespiteMeasuringBelowTheNominalMinimum()
        {
            // A three phase 6 A session measures around 4000 W, below the nominal 4140 W.
            // Taking the nominal value as the threshold would interrupt exactly the sessions
            // the surplus charging produces most of the time.
            var situation = Situation(insideChargingPower: 4007);

            Stabilize(T0, MinimumCurrentmA, situation).Should().Be(MinimumCurrentmA,
                "the session continues seamlessly, no start delay and no interruption");
            situation.InsideSwitchCycles.Should().Be(0, "no contactor cycle happened");
        }

        [Fact]
        public void SwitchCyclesAreCounted()
        {
            var situation = Situation();
            Stabilize(T0, MinimumCurrentmA, situation);
            Stabilize(T0.AddSeconds(30), MinimumCurrentmA, situation);
            situation.InsideSwitchCycles.Should().Be(1);

            Stabilize(T0.AddSeconds(60), 0, situation);
            Stabilize(T0.AddSeconds(340), 0, situation);
            Stabilize(T0.AddSeconds(650), MinimumCurrentmA, situation);
            Stabilize(T0.AddSeconds(700), MinimumCurrentmA, situation);
            situation.InsideSwitchCycles.Should().Be(2);
        }

        /// <summary>Brings the stabilizer into the charging state at the minimum current at T0 + 30 s.</summary>
        private void StartCharging()
        {
            Run(T0, MinimumCurrentmA);
            Run(T0.AddSeconds(30), MinimumCurrentmA).Should().Be(MinimumCurrentmA);
        }

        private int Run(DateTimeOffset now, int targetCurrentmA)
            => Stabilize(now, targetCurrentmA, Situation());

        private int Stabilize(DateTimeOffset now, int targetCurrentmA, ChargingSituation situation,
            ChargingSettings? settings = null)
        {
            var target = new ChargingResult(targetCurrentmA * 230 * 3 / 1000, 0, targetCurrentmA, 0);
            return stabilizer.Stabilize(target, situation, settings ?? Level3Settings(), now).InsideChargingCurrentmA;
        }

        private static ChargingSettings Level3Settings()
            => new() { ChargingLevel = 3, InsideChargingEnabled = true };

        private static ChargingSituation Situation(int gridPower = 0, int insideChargingPower = 0)
            => new()
            {
                InsideConnected = true,
                PowerFromGrid = gridPower,
                InsideCurrentChargingPower = insideChargingPower,
                BatterySupportedChargingActive = true,
            };
    }
}
