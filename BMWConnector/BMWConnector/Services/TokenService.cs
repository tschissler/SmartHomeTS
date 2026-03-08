using System.Text.Json;
using BMWConnector.Models;

namespace BMWConnector.Services;

/// <summary>
/// Manages BMW OAuth tokens stored in a Kubernetes Secret.
/// Reads and writes tokens via the Kubernetes API — no local files or kubectl binary needed.
/// The id_token is refreshed every 50 minutes in memory and persisted back to the Secret.
/// </summary>
public class TokenService
{
    private readonly VehicleConfig _config;
    private readonly KubernetesSecretStore _store;
    private readonly ILogger<TokenService> _log;
    private readonly HttpClient _http = new();

    private string? _idToken;
    private string? _refreshToken;
    private DateTime _idTokenRefreshedAt = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(50);

    public TokenService(VehicleConfig config, KubernetesSecretStore store, ILogger<TokenService> log)
    {
        _config = config;
        _store = store;
        _log = log;
    }

    public string IdToken => _idToken ?? throw new InvalidOperationException("Token not loaded yet.");

    public Task<bool> HasTokensAsync(CancellationToken ct = default)
        => _store.HasTokensAsync(_config.Name, ct);

    public async Task LoadAsync(CancellationToken ct)
    {
        (_idToken, _refreshToken) = await _store.GetTokensAsync(_config.Name, ct);
        _idTokenRefreshedAt = DateTime.UtcNow;
        _log.LogInformation("[{Vehicle}] Loaded tokens from Kubernetes Secret.", _config.Name);
    }

    public bool NeedsRefresh() => DateTime.UtcNow - _idTokenRefreshedAt >= RefreshInterval;

    public async Task RefreshAsync(CancellationToken ct)
    {
        _log.LogInformation("[{Vehicle}] Refreshing OAuth token...", _config.Name);

        string refreshToken = _refreshToken
            ?? throw new InvalidOperationException("Refresh token not loaded.");

        var form = new FormUrlEncodedContent([
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken),
            new("client_id", _config.ClientId),
        ]);

        var response = await _http.PostAsync("https://customer.bmwgroup.com/gcdm/oauth/token", form, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            bool isAuthError = (int)response.StatusCode is 400 or 401;
            string msg = $"Token refresh failed (HTTP {(int)response.StatusCode}): {body}";
            _log.LogError("[{Vehicle}] {Message}", _config.Name, msg);
            if (isAuthError)
                throw new BmwAuthExpiredException(msg);
            throw new HttpRequestException(msg);
        }

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        _idToken = doc.RootElement.GetProperty("id_token").GetString()
            ?? throw new InvalidOperationException("id_token missing from refresh response.");

        string newAccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";

        if (doc.RootElement.TryGetProperty("refresh_token", out var newRefresh))
            _refreshToken = newRefresh.GetString() ?? refreshToken;

        _idTokenRefreshedAt = DateTime.UtcNow;

        await _store.SaveTokensAsync(_config.Name, _idToken, newAccessToken, _refreshToken!, ct);
        _log.LogInformation("[{Vehicle}] Token refreshed and saved to Kubernetes Secret.", _config.Name);
    }
}

/// <summary>
/// Thrown when BMW's token endpoint rejects the refresh token (HTTP 400/401),
/// indicating the refresh token has expired and re-authentication is required.
/// </summary>
public class BmwAuthExpiredException(string message) : Exception(message);
