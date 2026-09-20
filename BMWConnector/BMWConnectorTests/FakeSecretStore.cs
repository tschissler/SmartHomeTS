using BMWConnector.Services;

namespace BMWConnectorTests;

/// <summary>
/// Stands in for the Kubernetes Secrets. Records every write so a test can assert the thing
/// that actually matters: that the production Secret would not have been touched.
/// </summary>
public sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _credentials = new();
    private readonly Dictionary<string, (string idToken, string refreshToken)> _tokens = new();

    public bool RunsInCluster { get; init; }

    /// <summary>Every credential write attempted, in order.</summary>
    public List<(string Key, string Value)> CredentialWrites { get; } = [];

    /// <summary>Every token write attempted, in order.</summary>
    public List<string> TokenWrites { get; } = [];

    public FakeSecretStore WithCredential(string key, string value)
    {
        _credentials[key] = value;
        return this;
    }

    public FakeSecretStore WithTokens(string vehicle, string idToken, string refreshToken = "refresh")
    {
        _tokens[vehicle] = (idToken, refreshToken);
        return this;
    }

    public Task<string?> GetCredentialAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_credentials.TryGetValue(key, out var v) ? v : null);

    public Task SaveCredentialAsync(string key, string value, CancellationToken ct = default)
    {
        CredentialWrites.Add((key, value));
        _credentials[key] = value;
        return Task.CompletedTask;
    }

    public Task<bool> HasTokensAsync(string vehicleName, CancellationToken ct = default)
        => Task.FromResult(_tokens.ContainsKey(vehicleName));

    public Task<(string idToken, string refreshToken)> GetTokensAsync(
        string vehicleName, CancellationToken ct = default)
        => _tokens.TryGetValue(vehicleName, out var t)
            ? Task.FromResult(t)
            : throw new InvalidOperationException($"No tokens for '{vehicleName}'.");

    public Task SaveTokensAsync(
        string vehicleName, string idToken, string accessToken, string refreshToken,
        CancellationToken ct = default)
    {
        TokenWrites.Add(vehicleName);
        _tokens[vehicleName] = (idToken, refreshToken);
        return Task.CompletedTask;
    }
}
