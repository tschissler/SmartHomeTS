namespace BMWConnector.Services;

/// <summary>
/// Access to the credentials and OAuth tokens the connector keeps in Kubernetes Secrets.
/// Extracted as an interface so the credential and token-age logic can be tested against
/// a fake — the production Secret must never be touched by a test run.
/// </summary>
public interface ISecretStore
{
    /// <summary>True when the process runs inside the cluster (in-cluster ServiceAccount).</summary>
    bool RunsInCluster { get; }

    Task<string?> GetCredentialAsync(string key, CancellationToken ct = default);

    Task SaveCredentialAsync(string key, string value, CancellationToken ct = default);

    Task<bool> HasTokensAsync(string vehicleName, CancellationToken ct = default);

    Task<(string idToken, string refreshToken)> GetTokensAsync(
        string vehicleName, CancellationToken ct = default);

    Task SaveTokensAsync(
        string vehicleName, string idToken, string accessToken, string refreshToken,
        CancellationToken ct = default);
}
