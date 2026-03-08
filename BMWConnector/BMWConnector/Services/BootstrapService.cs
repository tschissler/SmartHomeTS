using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BMWConnector.Models;

namespace BMWConnector.Services;

/// <summary>
/// Performs the one-time OAuth2 PKCE device code flow to obtain initial tokens,
/// then saves them to the Kubernetes Secret via the KubernetesSecretStore.
/// Run locally with: dotnet run -- --bootstrap BMW  (or Mini)
/// </summary>
public static class BootstrapService
{
    private const string DeviceCodeUrl = "https://customer.bmwgroup.com/gcdm/oauth/device/code";
    private const string TokenUrl      = "https://customer.bmwgroup.com/gcdm/oauth/token";
    private const string Scope         = "authenticate_user openid cardata:streaming:read";

    public static async Task RunAsync(VehicleConfig config, KubernetesSecretStore store)
    {
        Console.WriteLine($"=== Bootstrap: {config.Name} ===");
        Console.WriteLine();

        using var http = new HttpClient();

        var codeVerifier  = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        Console.WriteLine("Requesting device code...");
        var deviceResponse = await http.PostAsync(DeviceCodeUrl, new FormUrlEncodedContent([
            new("client_id",             config.ClientId),
            new("scope",                 Scope),
            new("code_challenge",        codeChallenge),
            new("code_challenge_method", "S256"),
        ]));

        var deviceBody = await deviceResponse.Content.ReadAsStringAsync();
        var deviceJson = JsonDocument.Parse(deviceBody);

        if (!deviceResponse.IsSuccessStatusCode
            || deviceJson.RootElement.TryGetProperty("error", out _))
        {
            string err  = deviceJson.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? "" : $"HTTP {(int)deviceResponse.StatusCode}";
            string desc = deviceJson.RootElement.TryGetProperty("error_description", out var d) ? d.GetString() ?? "" : deviceBody;
            Console.Error.WriteLine($"[{config.Name}] Device code request failed: {err}");
            Console.Error.WriteLine($"  {desc}");
            Console.Error.WriteLine();
            Console.Error.WriteLine($"  The CLIENT_ID for '{config.Name}' is likely wrong or missing.");
            Console.Error.WriteLine($"  --> See BMWConnector/SETUP.md, Steps 1 and 2.");
            Console.Error.WriteLine($"  --> Update the 'bmwconnector-credentials' Secret:");
            Console.Error.WriteLine($"      kubectl -n smarthome edit secret bmwconnector-credentials");
            return;
        }

        if (!deviceJson.RootElement.TryGetProperty("device_code", out var deviceCodeEl))
        {
            Console.Error.WriteLine($"[{config.Name}] Unexpected response from BMW — raw body:");
            Console.Error.WriteLine(deviceBody);
            Console.Error.WriteLine();
            Console.Error.WriteLine("  --> See BMWConnector/SETUP.md if you need help diagnosing this.");
            return;
        }

        var deviceCode = deviceCodeEl.GetString()!;
        var interval   = deviceJson.RootElement.GetProperty("interval").GetInt32();

        string verifyUrl;
        if (deviceJson.RootElement.TryGetProperty("verification_uri_complete", out var completeEl))
        {
            verifyUrl = completeEl.GetString()!;
        }
        else
        {
            string baseUrl  = deviceJson.RootElement.GetProperty("verification_uri").GetString()!;
            string userCode = deviceJson.RootElement.TryGetProperty("user_code", out var ucEl)
                ? ucEl.GetString()!
                : "";
            verifyUrl = string.IsNullOrEmpty(userCode) ? baseUrl : $"{baseUrl}?user_code={userCode}";
        }

        Console.WriteLine();
        Console.WriteLine("Open this URL in your browser and log in with your BMW account:");
        Console.WriteLine();
        Console.WriteLine($"  {verifyUrl}");
        Console.WriteLine();
        Console.WriteLine("Press ENTER once you see 'Anmeldung erfolgreich / Login successful'...");
        Console.ReadLine();

        Console.WriteLine("Polling for tokens...");
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval));

            var tokenResponse = await http.PostAsync(TokenUrl, new FormUrlEncodedContent([
                new("grant_type",   "urn:ietf:params:oauth:grant-type:device_code"),
                new("device_code",  deviceCode),
                new("client_id",    config.ClientId),
                new("code_verifier", codeVerifier),
            ]));

            var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());

            if (tokenResponse.IsSuccessStatusCode
                && tokenJson.RootElement.TryGetProperty("id_token", out _))
            {
                string idToken      = tokenJson.RootElement.GetProperty("id_token").GetString()!;
                string accessToken  = tokenJson.RootElement.GetProperty("access_token").GetString()!;
                string refreshToken = tokenJson.RootElement.GetProperty("refresh_token").GetString()!;

                await store.SaveTokensAsync(config.Name, idToken, accessToken, refreshToken);

                Console.WriteLine($"Tokens saved to Kubernetes Secret 'bmwconnector-{config.Name.ToLower()}-tokens'.");
                Console.WriteLine("Bootstrap complete. You can now start the service normally.");
                return;
            }

            if (tokenJson.RootElement.TryGetProperty("error", out var error))
            {
                string errorStr = error.GetString() ?? "";
                if (errorStr == "authorization_pending" || errorStr == "slow_down")
                {
                    Console.Write(".");
                    continue;
                }
                Console.WriteLine($"\nError: {errorStr}");
                return;
            }
        }
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
