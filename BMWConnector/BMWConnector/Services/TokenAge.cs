using System.Text.Json;

namespace BMWConnector.Services;

/// <summary>
/// Reads when the tokens currently stored in the Kubernetes Secret were issued.
///
/// BMW's refresh token lives two weeks and rotates: every refresh returns a fresh set and
/// restarts that clock, so the age of the original login says nothing about health. What
/// matters is the time since the last *successful* refresh, and the id_token's <c>iat</c>
/// claim carries exactly that.
///
/// Note that this deliberately reads the Secret rather than the in-memory token: if persisting
/// a rotated token ever fails, the service keeps running on its in-memory copy while the stored
/// one decays — and the stored one is all a restarted pod would have.
/// </summary>
public static class TokenAge
{
    /// <summary>
    /// The issue time from an id_token's <c>iat</c> claim, or null if the token cannot be
    /// read or does not carry the claim.
    /// </summary>
    public static DateTimeOffset? IssuedAtOf(string idToken) => ClaimOf(idToken, "iat");

    /// <summary>
    /// The interactive login time from the <c>auth_time</c> claim. Informational only — it
    /// survives every rotation and therefore never indicates an expiring token.
    /// </summary>
    public static DateTimeOffset? AuthTimeOf(string idToken) => ClaimOf(idToken, "auth_time");

    private static DateTimeOffset? ClaimOf(string idToken, string claim)
    {
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length < 2) return null;

            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            if (!payload.RootElement.TryGetProperty(claim, out var value)) return null;
            if (!value.TryGetInt64(out long seconds)) return null;

            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch
        {
            // A token we cannot parse is not worth failing over — the caller reports
            // "unknown" and the connector keeps running.
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string segment)
    {
        string padded = segment.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }
}
