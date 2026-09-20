using SharedContracts;

namespace ChargingController
{
    /// <summary>
    /// The share each source holds in the house's consumption at one moment. The three add up
    /// to exactly 1 by construction.
    /// </summary>
    public readonly record struct Quellenanteile(decimal Pv, decimal Batterie, decimal Netz);

    /// <summary>
    /// Attributes charging energy to PV, battery and grid, proportionally to the source mix of
    /// the whole house. The rule and the alternatives that were rejected are in
    /// Docs/Ladeprotokoll.md; this class is the rule, nothing else.
    /// </summary>
    /// <remarks>
    /// Everything here is a pure function of its arguments. The meter readings are state, but
    /// they are handed in and handed back rather than kept — so every edge case of the rule is
    /// testable without a broker, a clock or a wallbox.
    /// </remarks>
    public static class EnergyAttribution
    {
        /// <summary>
        /// Longest interval that may still be attributed. The control cycle runs every 5 s;
        /// jitter, a garbage collection or a short broker hiccup stay well below this.
        /// Anything longer is a real outage of the controller, and attributing it would mean
        /// booking minutes or hours of charging at whatever power happens to be measured now.
        /// Such an interval is skipped, and the gap to the box's own meter shows it — see the
        /// "Ausfallzeiten sind sichtbar statt kaschiert" paragraph of Docs/Ladeprotokoll.md.
        /// </summary>
        public static readonly TimeSpan MaximalesIntervall = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Number of decimals the meter readings are rounded to before they are handed on.
        /// Nine decimals of a kWh is a nanowatt hour: small enough that the rounding drift over
        /// a day of 5 s cycles stays around 10^-5 kWh, large enough to keep the retained JSON
        /// payload readable.
        /// </summary>
        private const int Nachkommastellen = 9;

        /// <summary>
        /// The share of PV, battery and grid in the current house consumption, or null when the
        /// interval must be skipped.
        /// </summary>
        /// <param name="verbrauchW">
        /// V — the whole house consumption in W, wallboxes included (Envoy channel PowerToHouse
        /// of M3, carried as ChargingSituation.HouseConsumptionPower). Null when no reading is
        /// available.
        /// </param>
        /// <param name="netzleistungW">
        /// PowerFromGrid in W, positive means drawing from the grid, negative means feeding in.
        /// </param>
        /// <param name="batterieleistungW">
        /// PowerFromBattery in W, positive means discharging, negative means charging.
        /// </param>
        /// <remarks>
        /// The denominator is the consumption, not the generation. As soon as the house feeds
        /// in or charges its battery, part of the PV generation never reaches the house, and
        /// raw PV power in the denominator would not add up to 100 %. The PV share is therefore
        /// taken as the remainder of the energy balance: P = max(0, V - N - B).
        /// <para>
        /// Normalised over S = P + N + B, not over V. Without measurement skew the two are the
        /// same. But the Envoy channels are read separately and can disagree for a moment — say
        /// V = 3000, N = 2000, B = 2000. Then P = 0, and with V as the denominator n + b = 1.33:
        /// the car would be booked 133 % of its charging power and the meters would run
        /// permanently high. With S as the denominator p + n + b = 1 is guaranteed by
        /// construction.
        /// </para>
        /// <para>
        /// Returns null rather than a guess whenever a reading is missing or the balance does
        /// not carry information (S = 0, which includes every V &lt;= 0). A skipped interval is
        /// a small error; a guessed attribution is a permanently wrong meter reading.
        /// </para>
        /// </remarks>
        public static Quellenanteile? BerechneAnteile(int? verbrauchW, int? netzleistungW, int? batterieleistungW)
        {
            if (verbrauchW is null || netzleistungW is null || batterieleistungW is null)
                return null;

            // Feeding into the grid is not a source the house draws from, and neither is a
            // charging battery. Both clamp to zero instead of turning into a negative share.
            decimal netz = Math.Max(0, netzleistungW.Value);
            decimal batterie = Math.Max(0, batterieleistungW.Value);
            decimal verbrauch = verbrauchW.Value;

            decimal pv = Math.Max(0, verbrauch - netz - batterie);
            decimal summe = pv + netz + batterie;

            if (summe <= 0)
                return null;

            return new Quellenanteile(pv / summe, batterie / summe, netz / summe);
        }

        /// <summary>
        /// Carries the meters of one box forward by one interval and returns the new reading.
        /// Pure: the previous reading goes in, the next one comes out.
        /// </summary>
        /// <param name="stand">The meter readings as they were after the previous cycle.</param>
        /// <param name="ladeleistungW">What the box reports it is charging with, in W.</param>
        /// <param name="anteile">
        /// The source mix from <see cref="BerechneAnteile"/>, or null when the mix could not be
        /// determined. In that case no energy is attributed — the interval shows up as
        /// unattributed energy, which is the intended behaviour.
        /// </param>
        /// <param name="dauer">
        /// The time that actually passed since the last update, not the nominal cycle interval.
        /// Ignored for attribution when it is not positive or longer than
        /// <see cref="MaximalesIntervall"/>.
        /// </param>
        /// <param name="zeitpunkt">When this cycle ran (UTC), written into the new reading.</param>
        /// <remarks>
        /// Charging time is counted even when the mix is unknown: it depends only on the power
        /// the box reports, which is measured, whereas the attribution is calculated. Skipping
        /// a measured fact because a calculated one is unavailable would make the charging time
        /// silently too short.
        /// <para>
        /// The momentary split powers are left at zero for a skipped interval. Zero is what is
        /// attributable, and the gap against the separately recorded AktuelleLadeleistung is
        /// exactly the visible unattributed part.
        /// </para>
        /// </remarks>
        public static Energieaufteilung Fortschreiben(
            Energieaufteilung stand,
            int ladeleistungW,
            Quellenanteile? anteile,
            TimeSpan dauer,
            DateTimeOffset zeitpunkt)
        {
            var neu = stand with { Zeitpunkt = zeitpunkt };

            bool intervallBrauchbar = dauer > TimeSpan.Zero && dauer <= MaximalesIntervall;
            bool laedt = ladeleistungW > Energieaufteilung.LadeleistungsschwelleW;

            if (intervallBrauchbar && laedt)
            {
                neu.LadezeitSekunden = stand.LadezeitSekunden + (long)Math.Round(dauer.TotalSeconds);
            }

            // A box that is not charging has nothing to attribute, and a negative reported power
            // is a fault of the box rather than energy flowing back.
            if (anteile is null || ladeleistungW <= 0)
            {
                neu.LadeleistungPvW = 0;
                neu.LadeleistungBatterieW = 0;
                neu.LadeleistungNetzW = 0;
                return neu;
            }

            var mix = anteile.Value;
            neu.LadeleistungPvW = Math.Round(mix.Pv * ladeleistungW, 2);
            neu.LadeleistungBatterieW = Math.Round(mix.Batterie * ladeleistungW, 2);
            neu.LadeleistungNetzW = Math.Round(mix.Netz * ladeleistungW, 2);

            if (!intervallBrauchbar)
                return neu;

            // W * s -> kWh
            decimal stunden = (decimal)dauer.TotalSeconds / 3600m;
            decimal energieKwh = ladeleistungW * stunden / 1000m;

            neu.EnergieLadungPvKwh = Runde(stand.EnergieLadungPvKwh + mix.Pv * energieKwh);
            neu.EnergieLadungBatterieKwh = Runde(stand.EnergieLadungBatterieKwh + mix.Batterie * energieKwh);
            neu.EnergieLadungNetzKwh = Runde(stand.EnergieLadungNetzKwh + mix.Netz * energieKwh);

            return neu;
        }

        private static decimal Runde(decimal wert) => Math.Round(wert, Nachkommastellen);
    }
}
