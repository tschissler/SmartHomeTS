using ChargingController;
using FluentAssertions;
using SharedContracts;

namespace ChargingControllerTests
{
    /// <summary>
    /// The attribution rule of Docs/Ladeprotokoll.md. These numbers end up in meters that are
    /// never reset, so the interesting cases are the ones where the rule refuses to answer.
    /// </summary>
    public class EnergyAttributionTests
    {
        private static readonly DateTimeOffset T0 = new(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(2));

        // --- the mix itself -------------------------------------------------------------

        [Fact]
        public void AtNightEverythingComesFromTheGrid()
        {
            var anteile = EnergyAttribution.BerechneAnteile(verbrauchW: 1000, netzleistungW: 1000, batterieleistungW: 0);

            anteile.Should().NotBeNull();
            anteile!.Value.Netz.Should().Be(1m);
            anteile.Value.Pv.Should().Be(0m);
            anteile.Value.Batterie.Should().Be(0m);
        }

        [Fact]
        public void WithoutGridAndBatteryTheRemainderIsAllPv()
        {
            var anteile = EnergyAttribution.BerechneAnteile(verbrauchW: 4000, netzleistungW: 0, batterieleistungW: 0);

            anteile!.Value.Pv.Should().Be(1m);
        }

        [Fact]
        public void FeedingIntoTheGridIsNotASourceTheHouseDrawsFrom()
        {
            // PowerFromGrid negative means exporting. The house still consumes 1000 W, and all
            // of it comes from the PV — a negative grid share would be nonsense.
            var anteile = EnergyAttribution.BerechneAnteile(verbrauchW: 1000, netzleistungW: -3000, batterieleistungW: 0);

            anteile!.Value.Pv.Should().Be(1m);
            anteile.Value.Netz.Should().Be(0m);
        }

        [Fact]
        public void AChargingBatteryIsNotASourceEither()
        {
            // PowerFromBattery negative means the battery is being charged. Those Watts leave
            // the house balance, they do not feed the car.
            var anteile = EnergyAttribution.BerechneAnteile(verbrauchW: 1000, netzleistungW: 1000, batterieleistungW: -2000);

            anteile!.Value.Netz.Should().Be(1m);
            anteile.Value.Batterie.Should().Be(0m);
            anteile.Value.Pv.Should().Be(0m);
        }

        [Fact]
        public void AMixedMomentSplitsProportionally()
        {
            // V = 3000, N = 1000, B = 500 -> P = 1500, S = 3000
            var anteile = EnergyAttribution.BerechneAnteile(verbrauchW: 3000, netzleistungW: 1000, batterieleistungW: 500);

            anteile!.Value.Pv.Should().Be(0.5m);
            anteile.Value.Netz.Should().BeApproximately(1m / 3m, 0.0000001m);
            anteile.Value.Batterie.Should().BeApproximately(1m / 6m, 0.0000001m);
        }

        [Fact]
        public void MeasurementSkewCannotPushTheSharesAboveOneHundredPercent()
        {
            // The case the concept document calls out: the Envoy channels disagree for a moment.
            // V = 3000 but N + B = 4000. With V as the denominator the shares would add up to
            // 1.33 and the meters would run permanently high. Normalising over S keeps them at 1.
            var anteile = EnergyAttribution.BerechneAnteile(verbrauchW: 3000, netzleistungW: 2000, batterieleistungW: 2000);

            anteile!.Value.Pv.Should().Be(0m);
            anteile.Value.Netz.Should().Be(0.5m);
            anteile.Value.Batterie.Should().Be(0.5m);
            Summe(anteile.Value).Should().Be(1m);
        }

        [Theory]
        [InlineData(1000, 1000, 0)]
        [InlineData(4000, 0, 0)]
        [InlineData(3000, 1000, 500)]
        [InlineData(3000, 2000, 2000)]
        [InlineData(5000, -2000, 3000)]
        [InlineData(7, 3, 1)]
        [InlineData(1, 0, 0)]
        public void TheThreeSharesAlwaysAddUpToExactlyOne(int v, int n, int b)
        {
            var anteile = EnergyAttribution.BerechneAnteile(v, n, b);

            Summe(anteile!.Value).Should().BeApproximately(1m, 0.0000000001m);
        }

        [Theory]
        [InlineData(0, 0, 0)]      // S == 0, nothing to normalise over
        [InlineData(-500, 0, 0)]   // V <= 0, the Envoy briefly reporting nonsense
        [InlineData(0, -100, -100)]
        public void AnUninformativeBalanceIsSkippedInsteadOfGuessed(int v, int n, int b)
        {
            EnergyAttribution.BerechneAnteile(v, n, b).Should().BeNull();
        }

        [Fact]
        public void AContradictoryBalanceStillYieldsAMixWhenASourceIsMeasured()
        {
            // V = 0 while the grid reports 1000 W: the channels contradict each other. S is
            // still 1000, so the rule answers instead of skipping — and it answers "all grid",
            // which is the only reading of those numbers that does not invent anything.
            var anteile = EnergyAttribution.BerechneAnteile(verbrauchW: 0, netzleistungW: 1000, batterieleistungW: 0);

            anteile!.Value.Netz.Should().Be(1m);
            anteile.Value.Pv.Should().Be(0m);
        }

        [Theory]
        [InlineData(null, 1000, 0)]
        [InlineData(1000, null, 0)]
        [InlineData(1000, 1000, null)]
        [InlineData(null, null, null)]
        public void AMissingReadingSkipsTheIntervalRatherThanAssumingZero(int? v, int? n, int? b)
        {
            EnergyAttribution.BerechneAnteile(v, n, b).Should().BeNull();
        }

        // --- carrying the meters forward ------------------------------------------------

        [Fact]
        public void EnergyIsAttributedInProportionToTheMix()
        {
            // Half PV, a third grid, a sixth battery; box charges at 3600 W for 10 s = 0.01 kWh
            var anteile = new Quellenanteile(Pv: 0.5m, Batterie: 0.25m, Netz: 0.25m);

            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), ladeleistungW: 3600, anteile, TimeSpan.FromSeconds(10), T0);

            stand.EnergieLadungPvKwh.Should().Be(0.005m);
            stand.EnergieLadungBatterieKwh.Should().Be(0.0025m);
            stand.EnergieLadungNetzKwh.Should().Be(0.0025m);
        }

        [Fact]
        public void TheSumOfTheThreeMetersEqualsTheEnergyTheBoxDelivered()
        {
            var anteile = EnergyAttribution.BerechneAnteile(11000, 4000, 1000)!.Value;

            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), ladeleistungW: 7200, anteile, TimeSpan.FromSeconds(5), T0);

            // 7200 W for 5 s = 0.01 kWh
            var summe = stand.EnergieLadungPvKwh + stand.EnergieLadungBatterieKwh + stand.EnergieLadungNetzKwh;
            summe.Should().BeApproximately(0.01m, 0.000000001m);
        }

        [Fact]
        public void TheMetersAccumulateAcrossIntervals()
        {
            var anteile = new Quellenanteile(1m, 0m, 0m);
            var stand = new Energieaufteilung();

            for (var i = 1; i <= 10; i++)
                stand = EnergyAttribution.Fortschreiben(stand, 3600, anteile, TimeSpan.FromSeconds(5), T0.AddSeconds(5 * i));

            // 3600 W for 50 s = 0.05 kWh
            stand.EnergieLadungPvKwh.Should().Be(0.05m);
        }

        [Fact]
        public void TheActualElapsedTimeIsUsedNotTheNominalCycle()
        {
            var anteile = new Quellenanteile(1m, 0m, 0m);

            var kurz = EnergyAttribution.Fortschreiben(new Energieaufteilung(), 3600, anteile, TimeSpan.FromSeconds(5), T0);
            var lang = EnergyAttribution.Fortschreiben(new Energieaufteilung(), 3600, anteile, TimeSpan.FromSeconds(7), T0);

            lang.EnergieLadungPvKwh.Should().Be(kurz.EnergieLadungPvKwh * 1.4m);
        }

        [Fact]
        public void AnIntervalLongerThanTheOutageLimitIsNotAttributed()
        {
            // The controller was down for an hour while the box charged autonomously. Booking
            // that hour at the power measured now would invent energy the controller never saw.
            var anteile = new Quellenanteile(1m, 0m, 0m);

            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), 11000, anteile, TimeSpan.FromHours(1), T0);

            stand.EnergieLadungPvKwh.Should().Be(0m);
            stand.LadezeitSekunden.Should().Be(0);
        }

        [Fact]
        public void TheFirstIntervalAfterARestartAttributesNothing()
        {
            var anteile = new Quellenanteile(1m, 0m, 0m);

            var stand = EnergyAttribution.Fortschreiben(new Energieaufteilung(), 11000, anteile, TimeSpan.Zero, T0);

            stand.EnergieLadungPvKwh.Should().Be(0m);
            stand.Zeitpunkt.Should().Be(T0);
        }

        [Fact]
        public void AnUnknownMixLeavesTheMetersWhereTheyAre()
        {
            var vorher = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), 3600, new Quellenanteile(1m, 0m, 0m), TimeSpan.FromSeconds(5), T0);

            var nachher = EnergyAttribution.Fortschreiben(vorher, 3600, anteile: null, TimeSpan.FromSeconds(5), T0.AddSeconds(5));

            nachher.EnergieLadungPvKwh.Should().Be(vorher.EnergieLadungPvKwh);
            nachher.EnergieLadungBatterieKwh.Should().Be(0m);
            nachher.EnergieLadungNetzKwh.Should().Be(0m);
        }

        [Fact]
        public void ASkippedIntervalReportsNoAttributableSplitPower()
        {
            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), 11000, anteile: null, TimeSpan.FromSeconds(5), T0);

            stand.LadeleistungPvW.Should().Be(0m);
            stand.LadeleistungBatterieW.Should().Be(0m);
            stand.LadeleistungNetzW.Should().Be(0m);
        }

        [Fact]
        public void TheMomentarySplitPowersAddUpToTheChargingPower()
        {
            var anteile = EnergyAttribution.BerechneAnteile(9000, 3000, 1500)!.Value;

            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), 7400, anteile, TimeSpan.FromSeconds(5), T0);

            (stand.LadeleistungPvW + stand.LadeleistungBatterieW + stand.LadeleistungNetzW)
                .Should().BeApproximately(7400m, 0.01m);
        }

        // --- charging time --------------------------------------------------------------

        [Fact]
        public void ChargingTimeCountsWhileTheBoxIsActuallyCharging()
        {
            var anteile = new Quellenanteile(1m, 0m, 0m);
            var stand = new Energieaufteilung();

            for (var i = 0; i < 3; i++)
                stand = EnergyAttribution.Fortschreiben(stand, 3600, anteile, TimeSpan.FromSeconds(5), T0.AddSeconds(5 * i));

            stand.LadezeitSekunden.Should().Be(15);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(12)]    // the idle noise the Keba reports
        [InlineData(100)]   // exactly at the threshold: "above 100 W", so this does not count
        public void IdleNoiseDoesNotCountAsChargingTime(int ladeleistungW)
        {
            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), ladeleistungW, new Quellenanteile(1m, 0m, 0m), TimeSpan.FromSeconds(5), T0);

            stand.LadezeitSekunden.Should().Be(0);
        }

        [Fact]
        public void JustAboveTheThresholdCounts()
        {
            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), 101, new Quellenanteile(1m, 0m, 0m), TimeSpan.FromSeconds(5), T0);

            stand.LadezeitSekunden.Should().Be(5);
        }

        [Fact]
        public void ChargingTimeIsCountedEvenWhenTheMixIsUnknown()
        {
            // The charging power is measured, the mix is calculated. Dropping a measured fact
            // because a calculated one is missing would make the charging time silently short.
            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), 7400, anteile: null, TimeSpan.FromSeconds(5), T0);

            stand.LadezeitSekunden.Should().Be(5);
            stand.EnergieLadungPvKwh.Should().Be(0m);
        }

        [Fact]
        public void ANegativeChargingPowerIsTreatedAsNoCharging()
        {
            var stand = EnergyAttribution.Fortschreiben(
                new Energieaufteilung(), -500, new Quellenanteile(1m, 0m, 0m), TimeSpan.FromSeconds(5), T0);

            stand.EnergieLadungPvKwh.Should().Be(0m);
            stand.LadezeitSekunden.Should().Be(0);
        }

        // --- a day of charging ----------------------------------------------------------

        [Fact]
        public void OverADayTheMetersMatchTheEnergyTheBoxDelivered()
        {
            // The acceptance criterion of backlog item 12, in miniature: an hour of 5 s cycles
            // at a changing source mix. MAX - MIN over the three meters has to match the energy
            // the box reports, because every interval was attributable.
            var stand = new Energieaufteilung();
            var zeit = T0;
            var boxEnergieKwh = 0m;

            for (var i = 0; i < 720; i++)
            {
                // The mix wanders from pure grid at night into pure PV at noon
                var netz = 4000 - i * 5;
                var anteile = EnergyAttribution.BerechneAnteile(11000, netz, 1000)!.Value;

                zeit = zeit.AddSeconds(5);
                stand = EnergyAttribution.Fortschreiben(stand, 7400, anteile, TimeSpan.FromSeconds(5), zeit);
                boxEnergieKwh += 7400m * 5m / 3600m / 1000m;
            }

            var summe = stand.EnergieLadungPvKwh + stand.EnergieLadungBatterieKwh + stand.EnergieLadungNetzKwh;
            summe.Should().BeApproximately(boxEnergieKwh, 0.000001m);
            stand.LadezeitSekunden.Should().Be(3600);
            stand.EnergieLadungPvKwh.Should().BeGreaterThan(0m);
            stand.EnergieLadungNetzKwh.Should().BeGreaterThan(0m);
            stand.EnergieLadungBatterieKwh.Should().BeGreaterThan(0m);
        }

        private static decimal Summe(Quellenanteile a) => a.Pv + a.Batterie + a.Netz;
    }
}
