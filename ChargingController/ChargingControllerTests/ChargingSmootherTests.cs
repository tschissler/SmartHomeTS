using ChargingController;
using FluentAssertions;
using SharedContracts;

namespace ChargingControllerTests
{
    public class ChargingSmootherTests
    {
        private static readonly DateTimeOffset T0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.FromHours(2));

        private readonly ChargingSmoother smoother = new(TimeSpan.FromSeconds(30));

        [Fact]
        public void TheFirstReadingIsTakenAsIsInsteadOfRampingUpFromZero()
        {
            var smoothed = smoother.Smooth(new ChargingSituation { PowerFromGrid = -4000 }, T0);

            smoothed.PowerFromGrid.Should().Be(-4000);
        }

        [Fact]
        public void AShortPeakIsDampedAway()
        {
            // 4000 W surplus as the starting point
            smoother.Smooth(new ChargingSituation { PowerFromGrid = -4000 }, T0);

            // The kettle switches on: the raw surplus collapses for one control cycle
            var peak = smoother.Smooth(new ChargingSituation { PowerFromGrid = 2000 }, T0.AddSeconds(5));

            // 5 s out of a 30 s time constant means roughly 15 % of the step
            peak.PowerFromGrid.Should().BeInRange(-3200, -3000);
        }

        [Fact]
        public void APersistentChangeIsFollowedWithinAFewTimeConstants()
        {
            smoother.Smooth(new ChargingSituation { PowerFromGrid = -4000 }, T0);

            var situation = new ChargingSituation { PowerFromGrid = 2000 };
            ChargingSituation smoothed = null!;
            for (var seconds = 5; seconds <= 120; seconds += 5)
            {
                smoothed = smoother.Smooth(situation, T0.AddSeconds(seconds));
            }

            smoothed.PowerFromGrid.Should().BeInRange(1850, 2000, "after four time constants the filter has caught up");
        }

        [Fact]
        public void TheRawReadingsAreNotModified()
        {
            var situation = new ChargingSituation
            {
                PowerFromGrid = -4000,
                PowerFromBattery = -1000,
                PowerFromPV = 5000,
                InsideCurrentChargingPower = 4140,
            };
            smoother.Smooth(situation, T0);
            smoother.Smooth(situation, T0.AddSeconds(5));

            situation.PowerFromGrid.Should().Be(-4000);
            situation.PowerFromBattery.Should().Be(-1000);
            // The UI and the energy attribution read the raw PV value; only the level 5
            // decision sees the filtered one.
            situation.PowerFromPV.Should().Be(5000);
            situation.InsideCurrentChargingPower.Should().Be(4140);
        }

        [Fact]
        public void HysteresisStateSurvivesTheCopy()
        {
            var situation = new ChargingSituation { BatterySupportedChargingActive = false, BatteryLevel = 42 };

            var smoothed = smoother.Smooth(situation, T0);

            smoothed.BatterySupportedChargingActive.Should().BeFalse();
            smoothed.BatteryLevel.Should().Be(42);
        }

        [Fact]
        public void AfterALongGapTheFilterAdoptsTheNewReading()
        {
            smoother.Smooth(new ChargingSituation { PowerFromGrid = -4000 }, T0);

            // Connection lost for ten minutes: holding on to the old value would be wrong
            var smoothed = smoother.Smooth(new ChargingSituation { PowerFromGrid = 2000 }, T0.AddMinutes(10));

            smoothed.PowerFromGrid.Should().Be(2000);
        }
    }
}
