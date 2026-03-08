using k8s;
using k8s.Autorest;
using k8s.Models;
using System.Net;
using System.Text;

namespace BMWConnector.Services;

/// <summary>
/// Reads and writes Kubernetes Secrets for BMW credentials and OAuth tokens.
/// Uses the in-cluster ServiceAccount when running in Kubernetes,
/// or ~/.kube/config when running locally.
/// </summary>
public class KubernetesSecretStore
{
    private const string Namespace = "smarthome";
    private const string CredentialsSecretName = "bmwconnector-credentials";

    private readonly IKubernetes _k8s;

    public KubernetesSecretStore()
    {
        var config = KubernetesClientConfiguration.IsInCluster()
            ? KubernetesClientConfiguration.InClusterConfig()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile();
        _k8s = new Kubernetes(config);
    }

    /// <summary>
    /// Reads a value from the bmwconnector-credentials Secret (CLIENT_ID, GCID).
    /// Returns null if the Secret or key does not exist.
    /// </summary>
    public async Task<string?> GetCredentialAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var secret = await _k8s.CoreV1.ReadNamespacedSecretAsync(
                CredentialsSecretName, Namespace, cancellationToken: ct);
            return GetString(secret, key);
        }
        catch { return null; }
    }

    /// <summary>
    /// Returns true if the vehicle's token Secret exists and contains both id and refresh tokens.
    /// </summary>
    public async Task<bool> HasTokensAsync(string vehicleName, CancellationToken ct = default)
    {
        try
        {
            var secret = await _k8s.CoreV1.ReadNamespacedSecretAsync(
                TokenSecretName(vehicleName), Namespace, cancellationToken: ct);
            return secret.Data?.ContainsKey("id_token.txt") == true
                && secret.Data?.ContainsKey("refresh_token.txt") == true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Reads id_token and refresh_token from the vehicle's token Secret.
    /// Throws if the Secret or required keys are missing.
    /// </summary>
    public async Task<(string idToken, string refreshToken)> GetTokensAsync(
        string vehicleName, CancellationToken ct = default)
    {
        string name = TokenSecretName(vehicleName);
        var secret = await _k8s.CoreV1.ReadNamespacedSecretAsync(name, Namespace, cancellationToken: ct);

        string idToken = GetString(secret, "id_token.txt")
            ?? throw new InvalidOperationException($"id_token.txt missing from Secret '{name}'.");
        string refreshToken = GetString(secret, "refresh_token.txt")
            ?? throw new InvalidOperationException($"refresh_token.txt missing from Secret '{name}'.");

        return (idToken, refreshToken);
    }

    /// <summary>
    /// Saves a single key/value into the bmwconnector-credentials Secret,
    /// merging with any existing keys. Creates the Secret if it doesn't exist.
    /// </summary>
    public async Task SaveCredentialAsync(string key, string value, CancellationToken ct = default)
    {
        Dictionary<string, byte[]> data;

        if (await ExistsAsync(CredentialsSecretName, ct))
        {
            var existing = await _k8s.CoreV1.ReadNamespacedSecretAsync(
                CredentialsSecretName, Namespace, cancellationToken: ct);
            data = existing.Data?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, byte[]>();
            data[key] = Encoding.UTF8.GetBytes(value);

            existing.Data = data;
            await _k8s.CoreV1.ReplaceNamespacedSecretAsync(
                existing, CredentialsSecretName, Namespace, cancellationToken: ct);
        }
        else
        {
            data = new Dictionary<string, byte[]> { [key] = Encoding.UTF8.GetBytes(value) };
            var secret = new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta { Name = CredentialsSecretName, NamespaceProperty = Namespace },
                Data = data,
            };
            await _k8s.CoreV1.CreateNamespacedSecretAsync(secret, Namespace, cancellationToken: ct);
        }
    }

    /// <summary>
    /// Creates or replaces the vehicle's token Secret with the given token values.
    /// </summary>
    public async Task SaveTokensAsync(
        string vehicleName, string idToken, string accessToken, string refreshToken,
        CancellationToken ct = default)
    {
        string name = TokenSecretName(vehicleName);
        var body = BuildTokenSecret(name, idToken, accessToken, refreshToken);

        if (await ExistsAsync(name, ct))
            await _k8s.CoreV1.ReplaceNamespacedSecretAsync(body, name, Namespace, cancellationToken: ct);
        else
            await _k8s.CoreV1.CreateNamespacedSecretAsync(body, Namespace, cancellationToken: ct);
    }

    private async Task<bool> ExistsAsync(string secretName, CancellationToken ct)
    {
        try
        {
            await _k8s.CoreV1.ReadNamespacedSecretAsync(secretName, Namespace, cancellationToken: ct);
            return true;
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static V1Secret BuildTokenSecret(
        string name, string idToken, string accessToken, string refreshToken) => new()
    {
        ApiVersion = "v1",
        Kind = "Secret",
        Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = Namespace },
        Data = new Dictionary<string, byte[]>
        {
            ["id_token.txt"]      = Encoding.UTF8.GetBytes(idToken),
            ["access_token.txt"]  = Encoding.UTF8.GetBytes(accessToken),
            ["refresh_token.txt"] = Encoding.UTF8.GetBytes(refreshToken),
        },
    };

    private static string? GetString(V1Secret secret, string key)
        => secret.Data?.TryGetValue(key, out var b) == true ? Encoding.UTF8.GetString(b) : null;

    private static string TokenSecretName(string vehicleName)
        => $"bmwconnector-{vehicleName.ToLower()}-tokens";
}
