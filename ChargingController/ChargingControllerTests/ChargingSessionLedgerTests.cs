using ChargingController;
using FluentAssertions;
using SharedContracts;

namespace ChargingControllerTests
{
    /// <summary>
    /// Where a charging session begins and ends, and what part of the three virtual meters
    /// belongs to it. The arithmetic is trivial; every rule worth testing is about a boundary.
    /// </summary>
    public class ChargingSessionLedgerTests
    {
        private static readonly DateTimeOffset T0 = new(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(2));

        /// <summary>The first moment the ledger acts without anything having been restored.</summary>
        private static readonly DateTimeOffset NachDerFrist = T0.AddSeconds(20);

        private static ChargingSessionLedger Buch() => new(T0, TimeSpan.FromSeconds(15));

        private static WallboxStatus Status(
            int? sitzungsId,
            DateTimeOffset? beginn = null,
            bool ausBoxZeit = false,
            int energieSitzungWh = 0)
            => new()
            {
                Zeitpunkt = T0,
                PlugStatus = sitzungsId is null ? 0 : WallboxStatus.PlugStatusFahrzeugVerbunden,
                SitzungsId = sitzungsId,
                SitzungsBeginn = beginn,
                SitzungsBeginnAusBoxZeit = ausBoxZeit,
                EnergieSitzungWh = energieSitzungWh,
            };

        private static Energieaufteilung Zaehler(
            decimal pv = 0m, decimal batterie = 0m, decimal netz = 0m, long ladezeit = 0)
            => new()
            {
                EnergieLadungPvKwh = pv,
                EnergieLadungBatterieKwh = batterie,
                EnergieLadungNetzKwh = netz,
                LadezeitSekunden = ladezeit,
            };

        // -------------------------------------------------------- the grace period

        [Fact]
        public void NothingHappensWhileTheRetainedSessionCouldStillArrive()
        {
            var buch = Buch();

            buch.Fortschreiben(LadeTopics.Garage, Status(5, T0), Zaehler(), T0.AddSeconds(5))
                .Should().BeEmpty("a session published now would overwrite the retained one that is still on its way");
            buch.Stand(LadeTopics.Garage).Should().BeNull();
        }

        // -------------------------------------------------------- where the start comes from

        [Fact]
        public void TheObservedPlugInEdgeWinsOverEverythingElse()
        {
            var buch = Buch();
            var eingesteckt = NachDerFrist.AddSeconds(-12);

            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage, Status(5, eingesteckt, ausBoxZeit: false), Zaehler(), NachDerFrist);

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].SitzungsId.Should().Be(5);
            veroeffentlicht[0].Beginn.Should().Be(eingesteckt);
            veroeffentlicht[0].BeginnGeschaetzt.Should().BeFalse();
            veroeffentlicht[0].Zustand.Should().Be(Ladesitzungszustand.Laufend);
            veroeffentlicht[0].Ende.Should().BeNull();
        }

        [Fact]
        public void TheBoxClockIsNotUsedForASessionTheLedgerWatchedAppear()
        {
            // SitzungsBeginnAusBoxZeit = true means the value came from the clock of the box,
            // which reports "timeQ": 0 — the untrustworthy source, not the trustworthy one. The
            // ledger saw this session appear itself, so its own clock beats that by a mile.
            var buch = Buch();
            buch.Fortschreiben(LadeTopics.Garage, Status(null), Zaehler(), NachDerFrist);

            var jetzt = NachDerFrist.AddSeconds(5);
            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage, Status(5, T0.AddYears(-3), ausBoxZeit: true), Zaehler(), jetzt);

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].Beginn.Should().Be(jetzt);
            veroeffentlicht[0].BeginnGeschaetzt.Should().BeFalse("the transition was observed, only a cycle late");
        }

        [Fact]
        public void AnInheritedSessionTakesThePlausibleBoxClockAndSaysSo()
        {
            // Nobody watched this plug go in: the process only came up afterwards. A box clock
            // that is at least plausible still knows more than "now" does — that the session is
            // older than this process — so it is used, and flagged as the estimate it is.
            var buch = Buch();
            var boxzeit = NachDerFrist.AddHours(-2);

            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage, Status(5, boxzeit, ausBoxZeit: true), Zaehler(), NachDerFrist);

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].Beginn.Should().Be(boxzeit);
            veroeffentlicht[0].BeginnGeschaetzt.Should().BeTrue();
        }

        [Theory]
        [InlineData(-400)]   // 400 days ago: no car hangs on a box that long
        [InlineData(3)]      // three days in the future
        public void AnImplausibleBoxClockIsRefused(int tageVersatz)
        {
            var buch = Buch();

            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage,
                Status(5, NachDerFrist.AddDays(tageVersatz), ausBoxZeit: true),
                Zaehler(),
                NachDerFrist);

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].Beginn.Should().Be(NachDerFrist);
            veroeffentlicht[0].BeginnGeschaetzt.Should().BeTrue("the session is older than this, we just cannot say how much");
        }

        // -------------------------------------------------------- the energy of a session

        [Fact]
        public void TheSessionCarriesTheDifferenceOfTheMetersNotTheirReading()
        {
            var buch = Buch();
            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 100m, ladezeit: 9000), NachDerFrist);

            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage,
                Status(5, NachDerFrist),
                Zaehler(pv: 100.01m, batterie: 0.002m, netz: 0.5m, ladezeit: 9005),
                NachDerFrist.AddSeconds(5));

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].EnergiePvKwh.Should().Be(0.01m);
            veroeffentlicht[0].EnergieBatterieKwh.Should().Be(0.002m);
            veroeffentlicht[0].EnergieNetzKwh.Should().Be(0.5m);
            veroeffentlicht[0].LadezeitSekunden.Should().Be(5, "charging time is a meter difference too");
        }

        [Fact]
        public void TheIntervalBeforeThePlugWentInIsNotTheSessionsEnergy()
        {
            var buch = Buch();
            buch.Fortschreiben(LadeTopics.Garage, Status(null), Zaehler(pv: 100m), NachDerFrist);

            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 100.01m), NachDerFrist.AddSeconds(5));

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].EnergiePvKwh.Should().Be(0m, "the other box, or nothing, drew that energy — not this session");
        }

        [Fact]
        public void TheIntervalInWhichThePlugIsPulledStillBelongsToTheEndingSession()
        {
            var buch = Buch();
            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 100m), NachDerFrist);
            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 100.01m), NachDerFrist.AddSeconds(5));

            var ende = NachDerFrist.AddSeconds(10);
            var veroeffentlicht = buch.Fortschreiben(LadeTopics.Garage, Status(null), Zaehler(pv: 100.02m), ende);

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].Zustand.Should().Be(Ladesitzungszustand.Beendet);
            veroeffentlicht[0].Ende.Should().Be(ende);
            veroeffentlicht[0].EnergiePvKwh.Should().Be(0.02m);
        }

        [Fact]
        public void TheBoxEnergyIsHeldFromTheLastRunningCycleNotFromTheOneThatReportsTheEnd()
        {
            // The box counts E pres per session and resets it for the next one. Reading it from
            // the status that already says "no session" would write a zero over a real charge.
            var buch = Buch();
            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist, energieSitzungWh: 0), Zaehler(), NachDerFrist);
            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist, energieSitzungWh: 3000), Zaehler(), NachDerFrist.AddSeconds(5));

            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage, Status(null, energieSitzungWh: 0), Zaehler(), NachDerFrist.AddSeconds(10));

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].EnergieBoxKwh.Should().Be(3m);
        }

        [Fact]
        public void MetersThatWentBackwardsAreNotBookedOntoTheSession()
        {
            var buch = Buch();
            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 100m, ladezeit: 50), NachDerFrist);

            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 99m, ladezeit: 40), NachDerFrist.AddSeconds(5));

            buch.Stand(LadeTopics.Garage)!.EnergiePvKwh.Should().Be(0m,
                "a meter that drops was restored or reset, and taking energy off a charge that happened is worse than missing an interval");
            buch.Stand(LadeTopics.Garage)!.LadezeitSekunden.Should().Be(0);
        }

        // -------------------------------------------------------- surviving a rollout

        [Fact]
        public void ARestoredSessionIsContinuedInsteadOfStartedAgain()
        {
            // This is the point of the retain mechanism: a rollout in the middle of a charge
            // must not cut the charge in two.
            var buch = Buch();
            var beginn = T0.AddHours(-1);
            buch.Wiederherstellen(LadeTopics.Garage, new Ladesitzung
            {
                SitzungsId = 5,
                Beginn = beginn,
                Zustand = Ladesitzungszustand.Laufend,
                EnergiePvKwh = 1.5m,
                EnergieBoxKwh = 1.6m,
                LadezeitSekunden = 1200,
                Zeitpunkt = T0.AddSeconds(-5),
            });

            var status = Status(5, beginn, energieSitzungWh: 1600);
            buch.Fortschreiben(LadeTopics.Garage, status, Zaehler(pv: 100m, ladezeit: 9000), T0.AddSeconds(1))
                .Should().BeEmpty("nothing changed yet, so the retained payload stays as it is");

            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage, status, Zaehler(pv: 100.01m, ladezeit: 9005), T0.AddSeconds(6));

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].SitzungsId.Should().Be(5);
            veroeffentlicht[0].Beginn.Should().Be(beginn);
            veroeffentlicht[0].EnergiePvKwh.Should().Be(1.51m);
            veroeffentlicht[0].LadezeitSekunden.Should().Be(1205);
        }

        [Fact]
        public void ASessionWhoseEndWasSleptThroughIsClosedAtTheLastMomentAnybodyKnewOfIt()
        {
            // Dating it "now" would invent a plugged-in time nobody observed. The timestamp of
            // the retained payload is the honest answer: that is when the controller last knew.
            var buch = Buch();
            var zuletztGesehen = T0.AddMinutes(-10);
            buch.Wiederherstellen(LadeTopics.Garage, new Ladesitzung
            {
                SitzungsId = 5,
                Beginn = T0.AddHours(-1),
                Zustand = Ladesitzungszustand.Laufend,
                EnergiePvKwh = 1.5m,
                Zeitpunkt = zuletztGesehen,
            });

            var veroeffentlicht = buch.Fortschreiben(LadeTopics.Garage, Status(null), Zaehler(), T0.AddSeconds(1));

            veroeffentlicht.Should().ContainSingle();
            veroeffentlicht[0].Zustand.Should().Be(Ladesitzungszustand.Beendet);
            veroeffentlicht[0].Ende.Should().Be(zuletztGesehen);
            veroeffentlicht[0].EnergiePvKwh.Should().Be(1.5m);
        }

        [Fact]
        public void NothingIsPublishedWhileNothingChanges()
        {
            var buch = Buch();
            var status = Status(5, NachDerFrist);
            buch.Fortschreiben(LadeTopics.Garage, status, Zaehler(pv: 100m), NachDerFrist);

            buch.Fortschreiben(LadeTopics.Garage, status, Zaehler(pv: 100m), NachDerFrist.AddSeconds(5))
                .Should().BeEmpty("a retained topic that repeats itself every five seconds says nothing new");
        }

        [Fact]
        public void AnEndAndTheNextStartInOneCycleAreBothPublished()
        {
            // Someone reseats the plug between two control cycles. Both statements have to go
            // out, and in this order — the topic holds one of them, the DataHub sees both.
            var buch = Buch();
            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 100m), NachDerFrist);

            var veroeffentlicht = buch.Fortschreiben(
                LadeTopics.Garage,
                Status(6, NachDerFrist.AddSeconds(3)),
                Zaehler(pv: 100.01m),
                NachDerFrist.AddSeconds(5));

            veroeffentlicht.Should().HaveCount(2);
            veroeffentlicht[0].SitzungsId.Should().Be(5);
            veroeffentlicht[0].Zustand.Should().Be(Ladesitzungszustand.Beendet);
            veroeffentlicht[0].EnergiePvKwh.Should().Be(0.01m, "the interval that just passed was still the old session");
            veroeffentlicht[1].SitzungsId.Should().Be(6);
            veroeffentlicht[1].Zustand.Should().Be(Ladesitzungszustand.Laufend);
            veroeffentlicht[1].EnergiePvKwh.Should().Be(0m);
        }

        [Fact]
        public void TheTwoBoxesAreKeptApart()
        {
            var buch = Buch();
            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 100m), NachDerFrist);
            buch.Fortschreiben(LadeTopics.Stellplatz, Status(9, NachDerFrist), Zaehler(pv: 7m), NachDerFrist);

            buch.Fortschreiben(LadeTopics.Garage, Status(5, NachDerFrist), Zaehler(pv: 100.01m), NachDerFrist.AddSeconds(5));

            buch.Stand(LadeTopics.Garage)!.EnergiePvKwh.Should().Be(0.01m);
            buch.Stand(LadeTopics.Stellplatz)!.SitzungsId.Should().Be(9);
            buch.Stand(LadeTopics.Stellplatz)!.EnergiePvKwh.Should().Be(0m);
        }
    }
}
