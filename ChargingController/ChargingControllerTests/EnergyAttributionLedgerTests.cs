using ChargingController;
using FluentAssertions;
using SharedContracts;

namespace ChargingControllerTests
{
    /// <summary>
    /// The state around the attribution rule: Δt, and the promise that a rollout does not
    /// reset the meters.
    /// </summary>
    public class EnergyAttributionLedgerTests
    {
        private static readonly DateTimeOffset T0 = new(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(2));
        private static readonly Quellenanteile NurPv = new(1m, 0m, 0m);

        private static EnergyAttributionLedger Ledger(TimeSpan? frist = null)
            => new(T0, frist ?? TimeSpan.FromSeconds(15));

        [Fact]
        public void NothingIsCountedWhileTheRetainedReadingCouldStillArrive()
        {
            var ledger = Ledger();

            ledger.Fortschreiben(LadeTopics.Garage, 7400, NurPv, T0.AddSeconds(5)).Should().BeNull();
        }

        [Fact]
        public void AfterTheGracePeriodTheMetersStartAtZero()
        {
            // First commissioning: nothing was ever published, so there is nothing to restore.
            var ledger = Ledger();

            var stand = ledger.Fortschreiben(LadeTopics.Garage, 7400, NurPv, T0.AddSeconds(20));

            stand.Should().NotBeNull();
            stand!.EnergieLadungPvKwh.Should().Be(0m);
        }

        [Fact]
        public void ARestoredReadingIsCarriedForwardInsteadOfRestartingAtZero()
        {
            // This is the point of the whole retain mechanism: six rollouts in a day must not
            // mean six meters back to zero.
            var ledger = Ledger();
            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung
            {
                EnergieLadungPvKwh = 123.5m,
                EnergieLadungNetzKwh = 45.25m,
                LadezeitSekunden = 9000,
                Zeitpunkt = T0.AddSeconds(-30),
            });

            // Two cycles: the first only sets the time base, the second attributes 5 s
            ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0.AddSeconds(1));
            var stand = ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0.AddSeconds(6));

            stand!.EnergieLadungPvKwh.Should().Be(123.505m);
            stand.EnergieLadungNetzKwh.Should().Be(45.25m);
            stand.LadezeitSekunden.Should().Be(9005);
        }

        [Fact]
        public void ARestoredReadingIsAcceptedImmediatelyWithoutWaitingOutTheGracePeriod()
        {
            var ledger = Ledger();
            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung { EnergieLadungPvKwh = 7m });

            ledger.Fortschreiben(LadeTopics.Garage, 7400, NurPv, T0.AddSeconds(1)).Should().NotBeNull();
        }

        [Fact]
        public void TheEchoOfOurOwnPublicationDoesNotWalkTheMetersBackwards()
        {
            // We subscribe to the topic we publish to, so every publication comes back. Adopting
            // it would undo whatever was counted in between.
            var ledger = Ledger();
            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung { EnergieLadungPvKwh = 100m });
            ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0.AddSeconds(1));
            var vorEcho = ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0.AddSeconds(6))!;

            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung { EnergieLadungPvKwh = 100m });

            ledger.Stand(LadeTopics.Garage).EnergieLadungPvKwh.Should().Be(vorEcho.EnergieLadungPvKwh);
        }

        [Fact]
        public void TheFirstCycleAfterAStartOnlySetsTheTimeBase()
        {
            // Δt is measured against the previous cycle. Without this the first interval would
            // stretch back to whenever the process happened to start.
            var ledger = Ledger();
            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung());

            var erster = ledger.Fortschreiben(LadeTopics.Garage, 11000, NurPv, T0.AddSeconds(10))!;

            erster.EnergieLadungPvKwh.Should().Be(0m);
            erster.LadezeitSekunden.Should().Be(0);
        }

        [Fact]
        public void DeltaTIsMeasuredBetweenCyclesNotTakenAsTheNominalInterval()
        {
            var ledger = Ledger();
            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung());
            ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0);

            // The loop ran late: 8 s instead of 5 s
            var stand = ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0.AddSeconds(8))!;

            stand.EnergieLadungPvKwh.Should().Be(3600m * 8m / 3600m / 1000m);
            stand.LadezeitSekunden.Should().Be(8);
        }

        [Fact]
        public void ASkippedIntervalDoesNotGetAttributedToTheFollowingOne()
        {
            // The mix was unknown for one cycle. That time is gone; carrying it into the next
            // interval would book it at a power that was never measured then.
            var ledger = Ledger();
            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung());
            ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0);
            ledger.Fortschreiben(LadeTopics.Garage, 3600, anteile: null, T0.AddSeconds(5));

            var stand = ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0.AddSeconds(10))!;

            // Only the last 5 s were attributed, not 10
            stand.EnergieLadungPvKwh.Should().Be(0.005m);
        }

        [Fact]
        public void TheTwoBoxesKeepSeparateMeters()
        {
            var ledger = Ledger();
            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung());
            ledger.Wiederherstellen(LadeTopics.Stellplatz, new Energieaufteilung());
            ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0);
            ledger.Fortschreiben(LadeTopics.Stellplatz, 0, NurPv, T0);

            ledger.Fortschreiben(LadeTopics.Garage, 3600, NurPv, T0.AddSeconds(5));
            ledger.Fortschreiben(LadeTopics.Stellplatz, 0, NurPv, T0.AddSeconds(5));

            ledger.Stand(LadeTopics.Garage).EnergieLadungPvKwh.Should().Be(0.005m);
            ledger.Stand(LadeTopics.Stellplatz).EnergieLadungPvKwh.Should().Be(0m);
        }

        [Fact]
        public void TheMetersNeverDecrease()
        {
            var ledger = Ledger();
            ledger.Wiederherstellen(LadeTopics.Garage, new Energieaufteilung());

            var letzte = 0m;
            var zeit = T0;
            foreach (var leistung in new[] { 0, 3600, 7400, 0, 11000, 50, 7400 })
            {
                zeit = zeit.AddSeconds(5);
                var stand = ledger.Fortschreiben(LadeTopics.Garage, leistung, NurPv, zeit)!;
                stand.EnergieLadungPvKwh.Should().BeGreaterThanOrEqualTo(letzte);
                letzte = stand.EnergieLadungPvKwh;
            }
        }
    }
}
