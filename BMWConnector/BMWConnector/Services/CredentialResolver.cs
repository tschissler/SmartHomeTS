using System.Globalization;

namespace BMWConnector.Services;

/// <summary>What to do with a credential after weighing environment against Kubernetes Secret.</summary>
public enum CredentialAction
{
    /// <summary>Use the value stored in the Kubernetes Secret.</summary>
    UseSecret,

    /// <summary>Use the environment variable for this process only; leave the Secret untouched.</summary>
    UseEnvironment,

    /// <summary>Use the environment variable and persist it — only on explicit opt-in.</summary>
    UseEnvironmentAndSave,

    /// <summary>Neither source holds a usable value.</summary>
    Missing,
}

/// <summary>The resolved value plus everything the operator should be told about how it was chosen.</summary>
public record CredentialDecision(CredentialAction Action, string Value, IReadOnlyList<string> Warnings);

/// <summary>
/// Decides where CLIENT_ID and GCID come from, and — more importantly — where they may go.
///
/// On 2026-09-20 a bootstrap run wrote the placeholders it had inherited from the user's
/// systemd environment into the production Secret <c>bmwconnector-credentials</c>, destroying
/// values that existed nowhere else. The rules below exist to make that impossible:
/// the Secret wins in the cluster, an environment variable may override locally but never
/// writes back on its own, and a value that does not look like a credential is refused outright.
///
/// This concerns credentials only. The OAuth tokens are a different matter entirely — they are
/// written back on every 50-minute refresh by design, see <see cref="ISecretStore.SaveTokensAsync"/>.
/// </summary>
public static class CredentialResolver
{
    /// <summary>Both CLIENT_ID and GCID are UUIDs in canonical 8-4-4-4-12 form.</summary>
    public static bool IsWellFormed(string value) => Guid.TryParseExact(value.Trim(), "D", out _);

    /// <summary>Markers of a template that was copied but never filled in.</summary>
    private static readonly string[] PlaceholderMarkers =
    [
        "REPLACE_ME", "REPLACEME", "CHANGE_ME", "CHANGEME", "PLACEHOLDER",
        "TODO", "XXX", "YOUR-", "YOUR_", "-HERE", "EXAMPLE", "DUMMY", "<", ">",
    ];

    public static bool IsPlaceholder(string value)
    {
        string upper = value.Trim().ToUpper(CultureInfo.InvariantCulture);
        return upper.Length > 0 && PlaceholderMarkers.Any(upper.Contains);
    }

    /// <summary>
    /// Why a value must not be used, or null if it is acceptable. Used for environment
    /// variables and for interactively entered values alike.
    /// </summary>
    public static string? RejectionReason(string key, string value)
    {
        if (IsPlaceholder(value))
            return $"{key}: refusing '{value.Trim()}' — that is an unfilled template placeholder, not a credential. "
                 + "A placeholder inherited from the user environment (`systemctl --user show-environment`) "
                 + "is how the production Secret was destroyed once; see BMWConnector/SETUP.md.";

        if (!IsWellFormed(value))
            return $"{key}: refusing {Describe(value)} — CLIENT_ID and GCID are UUIDs (8-4-4-4-12, 36 characters).";

        return null;
    }

    /// <summary>
    /// Renders a value for a log line without disclosing it. Recognised placeholders are shown
    /// verbatim — they carry no secret, and naming them is what lets an operator find their source.
    /// </summary>
    public static string Describe(string value)
    {
        string v = value.Trim();
        if (v.Length == 0) return "«empty»";
        if (IsPlaceholder(v)) return $"'{v}'";
        return v.Length >= 12
            ? $"{v[..4]}…{v[^4..]} ({v.Length} bytes)"
            : $"«{v.Length} bytes»";
    }

    /// <summary>
    /// Resolves one credential. The environment may only ever win outside the cluster, and only
    /// with a well-formed value; writing back requires an explicit opt-in on top of that.
    /// </summary>
    public static CredentialDecision Resolve(
        string key, string? envValue, string? secretValue, bool runningInCluster, bool writeBackOptIn)
    {
        var warnings = new List<string>();
        string env    = envValue?.Trim()    ?? "";
        string secret = secretValue?.Trim() ?? "";

        if (env.Length > 0)
        {
            string? rejection = RejectionReason(key, env);
            if (rejection != null)
            {
                warnings.Add(rejection);
                env = "";
            }
            else if (runningInCluster)
            {
                // In the cluster there is no legitimate reason to prefer an environment variable:
                // the Secret is the configured source, and an env var here can only be a leftover.
                warnings.Add($"{key}: ignoring the environment variable {Describe(env)} — "
                           + "inside the cluster the Kubernetes Secret is authoritative.");
                env = "";
            }
        }

        if (env.Length > 0)
        {
            if (secret.Length > 0 && secret != env)
                warnings.Add($"{key}: the environment variable {Describe(env)} overrides the Secret value "
                           + $"{Describe(secret)} for this process only — the Secret is left unchanged.");

            return writeBackOptIn
                ? new CredentialDecision(CredentialAction.UseEnvironmentAndSave, env, warnings)
                : new CredentialDecision(CredentialAction.UseEnvironment, env, warnings);
        }

        if (secret.Length > 0)
            return new CredentialDecision(CredentialAction.UseSecret, secret, warnings);

        return new CredentialDecision(CredentialAction.Missing, "", warnings);
    }
}
