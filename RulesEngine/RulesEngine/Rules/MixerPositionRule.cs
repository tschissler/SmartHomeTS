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
    private readonly HashSet<string> warmWaterStatusValues;
    private readonly TimeSpan maxStatusAge;

    public MixerPositionRule(IEnumerable<string> warmWaterStatusValues, TimeSpan maxStatusAge)
    {
        this.warmWaterStatusValues = warmWaterStatusValues
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToHashSet();
        this.maxStatusAge = maxStatusAge;
    }

    public MixerPosition Evaluate(string? faStatus, TimeSpan statusAge)
    {
        if (statusAge > maxStatusAge)
        {
            return MixerPosition.Open;
        }

        if (string.IsNullOrWhiteSpace(faStatus))
        {
            return MixerPosition.Open;
        }

        return warmWaterStatusValues.Contains(faStatus.Trim())
            ? MixerPosition.Closed
            : MixerPosition.Open;
    }
}
