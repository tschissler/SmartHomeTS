using System.Text;
using System.Text.Json;
using BMWConnector.Models;
using BMWConnector.Services;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace BMWConnectorTests;

public class TokenAgeTests
{
    [Fact]
    public void The_issue_time_comes_from_the_iat_claim()
    {
        var issued = new DateTimeOffset(2026, 9, 20, 8, 22, 18, TimeSpan.Zero);

        TokenAge.IssuedAtOf(Jwt(iat: issued)).Should().Be(issued);
    }

    [Fact]
    public void The_login_time_comes_from_the_auth_time_claim_and_is_unrelated_to_iat()
    {
        // Measured on the real Mini token: refreshed that morning, logged in months earlier.
        // This is why auth_time must not drive any warning.
        var issued   = new DateTimeOffset(2026, 9, 20, 8, 13, 32, TimeSpan.Zero);
        var loggedIn = new DateTimeOffset(2026, 3,  8, 12,  0, 34, TimeSpan.Zero);

        string token = Jwt(iat: issued, authTime: loggedIn);

        TokenAge.IssuedAtOf(token).Should().Be(issued);
        TokenAge.AuthTimeOf(token).Should().Be(loggedIn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b")]
    public void An_unreadable_token_yields_no_time_instead_of_throwing(string token)
    {
        TokenAge.IssuedAtOf(token).Should().BeNull();
    }

    [Fact]
    public void A_token_without_the_claim_yields_no_time()
    {
        TokenAge.IssuedAtOf(Jwt()).Should().BeNull();
    }

    internal static string Jwt(DateTimeOffset? iat = null, DateTimeOffset? authTime = null)
    {
        var claims = new Dictionary<string, object>();
        if (iat      is { } i) claims["iat"]       = i.ToUnixTimeSeconds();
        if (authTime is { } a) claims["auth_time"] = a.ToUnixTimeSeconds();

        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{Base64Url("{}"u8.ToArray())}.{payload}.signature";
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
