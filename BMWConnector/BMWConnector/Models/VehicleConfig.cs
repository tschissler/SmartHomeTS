using BMWConnector.Services;

namespace BMWConnector.Models;

public class VehicleConfig
{
    private const string K8sSecretName = "bmwconnector-credentials";
    private const string K8sNamespace  = "smarthome";

    public string Name { get; init; } = "";
    public string Gcid { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string OutputTopic { get; init; } = "";

    /// <summary>
    /// Loads the vehicle config. The Kubernetes Secret is the source of truth; an environment
    /// variable may override it on a development machine, but never writes back on its own —
    /// see <see cref="CredentialResolver"/> for why.
    /// </summary>
    /// <param name="allowCredentialWriteBack">
    /// Explicit opt-in (<c>--save-credentials</c>) to persist a well-formed environment value
    /// into the Secret. Off by default, and ignored inside the cluster.
    /// </param>
    /// <param name="allowInteractivePrompt">
    /// Whether a missing credential may be asked for on the console. False in the pod and in tests.
    /// </param>
    public static async Task<VehicleConfig> CreateAsync(
        string prefix, ISecretStore store, bool allowCredentialWriteBack = false,
        bool allowInteractivePrompt = false, CancellationToken ct = default)
    {
        return new VehicleConfig
        {
            Name        = prefix,
            Gcid        = await RequiredAsync($"{prefix}_GCID",      prefix, store, allowCredentialWriteBack, allowInteractivePrompt, ct),
            ClientId    = await RequiredAsync($"{prefix}_CLIENT_ID", prefix, store, allowCredentialWriteBack, allowInteractivePrompt, ct),
            OutputTopic = Env($"{prefix}_OUTPUT_TOPIC", $"data/charging/{prefix}"),
        };
    }

    private static async Task<string> RequiredAsync(
        string key, string vehicleName, ISecretStore store, bool allowCredentialWriteBack,
        bool allowInteractivePrompt, CancellationToken ct)
    {
        var decision = CredentialResolver.Resolve(
            key,
            envValue:         Env(key),
            secretValue:      await store.GetCredentialAsync(key, ct),
            runningInCluster: store.RunsInCluster,
            writeBackOptIn:   allowCredentialWriteBack);

        foreach (var warning in decision.Warnings)
            Console.Error.WriteLine($"[{vehicleName}] {warning}");

        switch (decision.Action)
        {
            case CredentialAction.UseSecret:
                Console.WriteLine($"[{vehicleName}] {key}: loaded from Kubernetes Secret '{K8sSecretName}'.");
                return decision.Value;

            case CredentialAction.UseEnvironment:
                Console.WriteLine($"[{vehicleName}] {key}: using environment variable "
                                + $"{CredentialResolver.Describe(decision.Value)} for this process. "
                                + $"Pass --save-credentials to also store it in '{K8sSecretName}'.");
                return decision.Value;

            case CredentialAction.UseEnvironmentAndSave:
                await store.SaveCredentialAsync(key, decision.Value, ct);
                Console.WriteLine($"[{vehicleName}] {key}: environment variable "
                                + $"{CredentialResolver.Describe(decision.Value)} saved to Kubernetes Secret "
                                + $"'{K8sSecretName}' (--save-credentials).");
                return decision.Value;
        }

        return await PromptAsync(key, vehicleName, store, allowInteractivePrompt, ct);
    }

    /// <summary>
    /// Last resort when neither source has the value: ask, validate the answer the same way an
    /// environment variable would be validated, and only then store it.
    /// </summary>
    private static async Task<string> PromptAsync(
        string key, string vehicleName, ISecretStore store, bool allowInteractivePrompt,
        CancellationToken ct)
    {
        if (allowInteractivePrompt)
        {
            Console.WriteLine();
            Console.WriteLine($"[{vehicleName}] '{key}' not found in environment variables or Kubernetes Secret '{K8sSecretName}'.");
            Console.WriteLine($"  --> See BMWConnector/SETUP.md, Step 1 for how to obtain this value.");
            Console.WriteLine();

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                Console.Write($"  Enter {key}: ");
                string entered = Console.ReadLine()?.Trim() ?? "";
                if (entered.Length == 0) break;

                string? rejection = CredentialResolver.RejectionReason(key, entered);
                if (rejection != null)
                {
                    Console.Error.WriteLine($"[{vehicleName}] {rejection}");
                    continue;
                }

                await store.SaveCredentialAsync(key, entered, ct);
                Console.WriteLine($"[{vehicleName}] {key}: saved to Kubernetes Secret '{K8sSecretName}'.");
                return entered;
            }
        }

        throw new MissingCredentialException($"""
            [{vehicleName}] Missing configuration: '{key}'
            Neither the environment variable nor the Kubernetes Secret '{K8sSecretName}' contains a usable value.

            --> See BMWConnector/SETUP.md, Steps 1 and 2, for how to obtain and store these values.

            Quick fix — create the credentials Secret (once for all vehicles):

              kubectl -n {K8sNamespace} create secret generic {K8sSecretName} \
                --from-literal=BMW_CLIENT_ID="<from BMW CarData Developer Portal>" \
                --from-literal=BMW_GCID="<your BMW account UUID>" \
                --from-literal=Mini_CLIENT_ID="<from BMW CarData Developer Portal>" \
                --from-literal=Mini_GCID="<your MINI account UUID>"

            """);
    }

    private static string Env(string key, string fallback = "")
        => Environment.GetEnvironmentVariable(key) ?? fallback;
}

/// <summary>
/// Thrown when a credential is available from neither the environment nor the Kubernetes Secret.
/// Carries the full operator guidance as its message.
/// </summary>
public class MissingCredentialException(string message) : Exception(message);
