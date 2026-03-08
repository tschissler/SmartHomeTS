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
    /// Loads vehicle config from env vars first, falling back to the k8s credentials Secret.
    /// Exits with a helpful message if neither source has the required values.
    /// </summary>
    public static async Task<VehicleConfig> CreateAsync(
        string prefix, KubernetesSecretStore store, CancellationToken ct = default)
    {
        return new VehicleConfig
        {
            Name        = prefix,
            Gcid        = await RequiredAsync($"{prefix}_GCID",      prefix, store, ct),
            ClientId    = await RequiredAsync($"{prefix}_CLIENT_ID",  prefix, store, ct),
            OutputTopic = Env($"{prefix}_OUTPUT_TOPIC", $"data/charging/{prefix}"),
        };
    }

    private static async Task<string> RequiredAsync(
        string key, string vehicleName, KubernetesSecretStore store, CancellationToken ct)
    {
        string value = Env(key);
        if (!string.IsNullOrEmpty(value))
        {
            Console.WriteLine($"[{vehicleName}] {key}: using environment variable, saving to Kubernetes Secret...");
            await store.SaveCredentialAsync(key, value, ct);
            Console.WriteLine($"[{vehicleName}] {key}: saved to Kubernetes Secret '{K8sSecretName}'.");
            return value;
        }

        value = await store.GetCredentialAsync(key, ct) ?? "";
        if (!string.IsNullOrEmpty(value))
        {
            Console.WriteLine($"[{vehicleName}] {key}: loaded from Kubernetes Secret '{K8sSecretName}'.");
            return value;
        }

        if (!Console.IsInputRedirected)
        {
            Console.WriteLine();
            Console.WriteLine($"[{vehicleName}] '{key}' not found in environment variables or Kubernetes Secret '{K8sSecretName}'.");
            Console.WriteLine($"  --> See BMWConnector/SETUP.md, Step 1 for how to obtain this value.");
            Console.WriteLine();
            Console.Write($"  Enter {key}: ");
            value = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(value))
            {
                await store.SaveCredentialAsync(key, value, ct);
                Console.WriteLine($"[{vehicleName}] {key}: saved to Kubernetes Secret '{K8sSecretName}'.");
                return value;
            }
        }

        Console.Error.WriteLine($"""
            [{vehicleName}] Missing configuration: '{key}'
            Neither the environment variable nor the Kubernetes Secret '{K8sSecretName}' contains this value.

            --> See BMWConnector/SETUP.md, Steps 1 and 2, for how to obtain and store these values.

            Quick fix — create the credentials Secret (once for all vehicles):

              kubectl -n {K8sNamespace} create secret generic {K8sSecretName} \
                --from-literal=BMW_CLIENT_ID="<from BMW CarData Developer Portal>" \
                --from-literal=BMW_GCID="<your BMW account UUID>" \
                --from-literal=Mini_CLIENT_ID="<from BMW CarData Developer Portal>" \
                --from-literal=Mini_GCID="<your MINI account UUID>"

            """);
        Environment.Exit(1);
        return ""; // unreachable
    }

    private static string Env(string key, string fallback = "")
        => Environment.GetEnvironmentVariable(key) ?? fallback;
}
