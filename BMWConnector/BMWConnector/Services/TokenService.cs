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
    private readonly ISecretStore _store;
    private readonly ILogger<TokenService> _log;
    private readonly HttpClient _http;
    private readonly TimeSpan _persistBackoff;

    private string? _idToken;
    private string? _refreshToken;
    private DateTime _idTokenRefreshedAt = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(50);
    private const int PersistAttempts = 4;

    /// <param name="handler">Test seam for BMW's token endpoint; null uses a real HttpClient.</param>
    /// <param name="persistBackoff">Test seam for the retry delay between Secret writes.</param>
    public TokenService(
        VehicleConfig config, ISecretStore store, ILogger<TokenService> log,
        HttpMessageHandler? handler = null, TimeSpan? persistBackoff = null)
    {
        _config = config;
        _store = store;
        _log = log;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _persistBackoff = persistBackoff ?? TimeSpan.FromSeconds(2);
    }

    public string IdToken => _idToken ?? throw new InvalidOperationException("Token not loaded yet.");

    public Task<bool> HasTokensAsync(CancellationToken ct = default)
        => _store.HasTokensAsync(_config.Name, ct);

    public async Task LoadAsync(CancellationToken ct)
    {
        (_idToken, _refreshToken) = await _store.GetTokensAsync(_config.Name, ct);

        // Date the loaded token by when it was issued, not by when this pod happened to start:
        // an id_token lives an hour, so a pod starting on a 55-minute-old one must refresh in
        // five minutes, not in fifty.
        var issuedAt = TokenAge.IssuedAtOf(_idToken);
        _idTokenRefreshedAt = issuedAt?.UtcDateTime ?? DateTime.UtcNow;

        _log.LogInformation("[{Vehicle}] Loaded tokens from Kubernetes Secret (issued {IssuedAt}).",
            _config.Name, issuedAt?.ToString("yyyy-MM-dd HH:mm 'UTC'") ?? "at an unknown time");
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

        string newIdToken = doc.RootElement.GetProperty("id_token").GetString()
            ?? throw new InvalidOperationException("id_token missing from refresh response.");
        string newAccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
        string newRefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var newRefresh)
            ? newRefresh.GetString() ?? refreshToken
            : refreshToken;

        // BMW rotates the refresh token: the moment the response above arrived, the token that
        // is still in the Secret became worthless. Persist the new set BEFORE adopting it, so
        // the Secret never lags behind what this process is using.
        bool persisted = await TryPersistAsync(newIdToken, newAccessToken, newRefreshToken, ct);

        _idToken            = newIdToken;
        _refreshToken       = newRefreshToken;
        _idTokenRefreshedAt = DateTime.UtcNow;

        if (persisted)
            _log.LogInformation("[{Vehicle}] Token refreshed and saved to Kubernetes Secret.", _config.Name);
        else
            _log.LogError(
                "[{Vehicle}] Token refreshed but NOT saved to Kubernetes Secret. The only valid "
              + "refresh token now lives in this process's memory — a pod restart would lose it and "
              + "force an interactive re-bootstrap. The next refresh retries in ~50 minutes; if that "
              + "keeps failing, check the pod's RBAC on Secret 'bmwconnector-{Secret}-tokens'.",
                _config.Name, _config.Name.ToLower());
    }

    /// <summary>
    /// Writes the rotated token set to the Secret, retrying a transient failure rather than
    /// shrugging it off. Returns false once every attempt has failed.
    /// </summary>
    private async Task<bool> TryPersistAsync(
        string idToken, string accessToken, string refreshToken, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= PersistAttempts; attempt++)
        {
            try
            {
                await _store.SaveTokensAsync(_config.Name, idToken, accessToken, refreshToken, ct);
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(
                    "[{Vehicle}] Saving the refreshed tokens to the Kubernetes Secret failed "
                  + "(attempt {Attempt}/{Total}): {Message}",
                    _config.Name, attempt, PersistAttempts, ex.Message);

                if (attempt == PersistAttempts) return false;
                await Task.Delay(_persistBackoff * attempt, ct);
            }
        }

        return false;
    }
}

/// <summary>
/// Thrown when BMW's token endpoint rejects the refresh token (HTTP 400/401),
/// indicating the refresh token has expired and re-authentication is required.
/// </summary>
public class BmwAuthExpiredException(string message) : Exception(message);
