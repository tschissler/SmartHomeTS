namespace RulesEngine.Rules;

/// <summary>
/// Normalized whitelist of FA_Status values shared by all rules: entries and
/// probed values are trimmed, empty entries dropped. A missing/blank status
/// never matches — matching is the exceptional case, not matching the default.
/// </summary>
public class StatusWhitelist
{
    private readonly HashSet<string> values;

    public StatusWhitelist(IEnumerable<string> values)
    {
        this.values = values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToHashSet();
    }

    public bool Matches(string? status)
        => !string.IsNullOrWhiteSpace(status) && values.Contains(status.Trim());
}
