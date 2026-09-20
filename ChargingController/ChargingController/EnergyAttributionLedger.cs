using SharedContracts;

namespace ChargingController
{
    /// <summary>
    /// Keeps the three virtual meters of every wallbox and carries them forward once per
    /// control cycle. The arithmetic itself lives in <see cref="EnergyAttribution"/>; what this
    /// class adds is the state around it — the readings, the time base for Δt, and the
    /// restore-before-you-count rule.
    /// </summary>
    /// <remarks>
    /// The meters must never restart at zero, so the ledger refuses to count for a box until
    /// either its retained reading has arrived or a grace period has passed. Without that rule
    /// a rollout would publish a fresh zero over the retained topic before the broker had
    /// replayed the old value — and the service was rolled out six times in a single day while
    /// this backlog was being worked through.
    /// </remarks>
    public sealed class EnergyAttributionLedger
    {
        /// <summary>
        /// How long the ledger waits for its own retained topic before it accepts that there is
        /// nothing to restore and starts at zero. Retained messages arrive within milliseconds
        /// of subscribing, so this is generous on purpose: the cost of waiting is a few seconds
        /// of unattributed charging, the cost of not waiting is a meter reset to zero.
        /// </summary>
        public static readonly TimeSpan Wiederherstellungsfrist = TimeSpan.FromSeconds(15);

        private sealed class Zaehler
        {
            public Energieaufteilung Stand = new();
            public DateTimeOffset? LetzteFortschreibung;
            public bool Wiederhergestellt;
            public bool StartGemeldet;
        }

        private readonly Dictionary<string, Zaehler> zaehler = new();
        private readonly DateTimeOffset gestartet;
        private readonly TimeSpan frist;

        public EnergyAttributionLedger(DateTimeOffset gestartet, TimeSpan? wiederherstellungsfrist = null)
        {
            this.gestartet = gestartet;
            frist = wiederherstellungsfrist ?? Wiederherstellungsfrist;
        }

        /// <summary>
        /// Takes a reading that arrived on the box's own retained topic. Only the first one
        /// counts: from the second message on this is the ledger hearing its own publication
        /// echoed back, and adopting that would at best change nothing and at worst walk the
        /// meters backwards by one cycle.
        /// </summary>
        public void Wiederherstellen(string wallbox, Energieaufteilung stand)
        {
            var z = Hole(wallbox);
            if (z.Wiederhergestellt)
                return;

            z.Stand = stand;
            z.Wiederhergestellt = true;
            z.StartGemeldet = true;
            Console.WriteLine($"Energieaufteilung {wallbox}: restored meters from the retained topic " +
                $"(PV {stand.EnergieLadungPvKwh:F3} kWh, battery {stand.EnergieLadungBatterieKwh:F3} kWh, " +
                $"grid {stand.EnergieLadungNetzKwh:F3} kWh, charging time {stand.LadezeitSekunden} s, " +
                $"last written {stand.Zeitpunkt:u})");
        }

        /// <summary>
        /// Whether the ledger may count for this box yet — see <see cref="Wiederherstellungsfrist"/>.
        /// </summary>
        public bool Bereit(string wallbox, DateTimeOffset jetzt)
        {
            var z = Hole(wallbox);
            if (z.Wiederhergestellt)
                return true;
            if (jetzt - gestartet < frist)
                return false;

            if (!z.StartGemeldet)
            {
                z.StartGemeldet = true;
                Console.WriteLine($"Energieaufteilung {wallbox}: no retained meter reading arrived within " +
                    $"{frist.TotalSeconds:F0} s, starting the meters at zero. This is expected on first " +
                    "commissioning; at any other time it means the retained topic was lost and the history " +
                    "of this box now has a step in it.");
            }
            return true;
        }

        /// <summary>
        /// Carries the meters of one box forward and returns the new reading to be published,
        /// or null while the ledger is still waiting for its retained state.
        /// </summary>
        /// <param name="ladeleistungW">What the box reports it is charging with, in W.</param>
        /// <param name="anteile">The source mix, or null when the interval must be skipped.</param>
        /// <param name="jetzt">When this control cycle ran (UTC).</param>
        /// <remarks>
        /// Δt is measured against the previous call for this box, not against the nominal cycle
        /// interval — the loop runs on a <c>Task.Delay</c> and drifts, and a skipped cycle would
        /// otherwise be silently booked as if it had been on time. The very first call after a
        /// restart only sets the time base and attributes nothing: the interval before it
        /// belongs to a run of the service that is no longer around to describe it.
        /// </remarks>
        public Energieaufteilung? Fortschreiben(
            string wallbox,
            int ladeleistungW,
            Quellenanteile? anteile,
            DateTimeOffset jetzt)
        {
            if (!Bereit(wallbox, jetzt))
                return null;

            var z = Hole(wallbox);
            var dauer = z.LetzteFortschreibung is null ? TimeSpan.Zero : jetzt - z.LetzteFortschreibung.Value;

            z.Stand = EnergyAttribution.Fortschreiben(z.Stand, ladeleistungW, anteile, dauer, jetzt);
            // Advanced even for a skipped interval: the time is gone either way, and carrying it
            // into the next interval would attribute it at a power that was never measured then.
            z.LetzteFortschreibung = jetzt;
            return z.Stand;
        }

        /// <summary>The current reading of one box, for diagnostics and tests.</summary>
        public Energieaufteilung Stand(string wallbox) => Hole(wallbox).Stand;

        private Zaehler Hole(string wallbox)
        {
            if (!zaehler.TryGetValue(wallbox, out var z))
            {
                z = new Zaehler();
                zaehler[wallbox] = z;
            }
            return z;
        }
    }
}
