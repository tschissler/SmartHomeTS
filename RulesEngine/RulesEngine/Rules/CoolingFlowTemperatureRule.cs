namespace RulesEngine.Rules;

public enum PulseDirection
{
    Open,
    Close
}

public record MixerPulse(PulseDirection Direction, int Seconds);

/// <summary>
/// Dreipunkt-Schrittregler: trims the FBHZ mixer so the circuit flow
/// temperature stays near the target. Too cold causes condensation (dew
/// point watchdog blocks the heat pump), too warm wastes cooling capacity.
///
/// Regulates whenever the base rule leaves the mixer open — warm water
/// preparation (base rule "close") has priority, because then the heat pump
/// flow is hot and opening would warm the circuit instead of cooling it.
///
/// The pulse direction assumes the cooling season: open = more cold buffer
/// water = colder. During heating the same logic simply converges to fully
/// open (warm flow reads as "too warm" → open pulses), which is the legacy
/// mixer behavior; proper heating-mode regulation is a later step.
///
/// Returns a short pulse towards close (less cold water) or open (more cold
/// water), or null when the mixer must not move. Fail-safe null: pulses are
/// emitted ONLY while the circuit pump is running and all inputs are fresh —
/// in every other case the mixer stays wherever the base rule put it.
/// </summary>
public class CoolingFlowTemperatureRule
{
    private readonly TimeSpan maxInputAge;
    private readonly double deadbandKelvin;
    private readonly double pulseSecondsPerKelvin;
    private readonly int minPulseSeconds;
    private readonly int maxPulseSeconds;

    public CoolingFlowTemperatureRule(
        TimeSpan maxInputAge,
        double deadbandKelvin,
        double pulseSecondsPerKelvin,
        int minPulseSeconds,
        int maxPulseSeconds)
    {
        this.maxInputAge = maxInputAge;
        this.deadbandKelvin = deadbandKelvin;
        this.pulseSecondsPerKelvin = pulseSecondsPerKelvin;
        this.minPulseSeconds = minPulseSeconds;
        this.maxPulseSeconds = maxPulseSeconds;
    }

    // targetTemperature is an input, not construction config: it is
    // adjustable at runtime via the retained config/RulesEngine message
    public MixerPulse? Evaluate(
        MixerPosition? basePosition,
        bool? pumpRunning, TimeSpan pumpStatusAge,
        double? flowTemperature, TimeSpan flowTemperatureAge,
        double targetTemperature)
    {
        // The base rule owns the mixer whenever it does not say "open"
        if (basePosition != MixerPosition.Open)
        {
            return null;
        }

        if (pumpStatusAge > maxInputAge || flowTemperatureAge > maxInputAge)
        {
            return null;
        }

        // Without circulation the sensor reads standing water, not the circuit
        if (pumpRunning != true)
        {
            return null;
        }

        if (flowTemperature is not double flow)
        {
            return null;
        }

        var error = flow - targetTemperature;
        if (Math.Abs(error) <= deadbandKelvin)
        {
            return null;
        }

        var seconds = (int)Math.Round(Math.Abs(error) * pulseSecondsPerKelvin);
        seconds = Math.Clamp(seconds, minPulseSeconds, maxPulseSeconds);

        // Open = more cold buffer water (lowers flow temp), close = less
        return new MixerPulse(error > 0 ? PulseDirection.Open : PulseDirection.Close, seconds);
    }
}
