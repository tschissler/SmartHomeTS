namespace RulesEngine.Rules;

public enum MixerPosition
{
    Open,
    Closed
}

/// <summary>
/// Decides the target position of the heating circuit mixers based on the
/// heat pump operating status (FA_Status from the CAN gateway).
///
/// Whitelist logic with fail-safe "open": the mixers close ONLY while the
/// heat pump reports one of the configured Warmwasser status values AND that
/// status is fresh. Unknown values, an empty whitelist or a stale status
/// always result in open — open only costs efficiency, never comfort.
/// </summary>
public class MixerPositionRule
{
    private readonly StatusWhitelist warmWaterStatus;
    private readonly TimeSpan maxStatusAge;

    public MixerPositionRule(IEnumerable<string> warmWaterStatusValues, TimeSpan maxStatusAge)
    {
        warmWaterStatus = new StatusWhitelist(warmWaterStatusValues);
        this.maxStatusAge = maxStatusAge;
    }

    public MixerPosition Evaluate(string? faStatus, TimeSpan statusAge)
    {
        if (statusAge > maxStatusAge)
        {
            return MixerPosition.Open;
        }

        return warmWaterStatus.Matches(faStatus)
            ? MixerPosition.Closed
            : MixerPosition.Open;
    }
}
