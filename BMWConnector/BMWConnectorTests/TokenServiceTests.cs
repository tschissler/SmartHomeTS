using System.Net;
using System.Text.Json;
using BMWConnector.Models;
using BMWConnector.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace BMWConnectorTests;

/// <summary>
/// The rotation contract: BMW invalidates the old refresh token the moment it hands out a new
/// set, so the new one must reach the Secret or the access is one pod restart from being lost.
/// </summary>
public class TokenServiceTests
{
    private static readonly VehicleConfig Bmw = new() { Name = "BMW", ClientId = "client", Gcid = "gcid" };

    [Fact]
    public async Task A_loaded_token_is_dated_by_its_issue_time_not_by_the_pod_start()
    {
        // A pod starting on a 55-minute-old id_token must refresh within minutes: the token
        // lives an hour. Dating it "now" would wait the full 50 and connect with a dead token.
        var store = new FakeSecretStore()
            .WithTokens("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow.AddMinutes(-55)));
        var service = NewService(store, out _);

        await service.LoadAsync(CancellationToken.None);

        service.NeedsRefresh().Should().BeTrue();
    }

    [Fact]
    public async Task A_freshly_issued_token_does_not_need_refreshing_yet()
    {
        var store = new FakeSecretStore()
            .WithTokens("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow.AddMinutes(-5)));
        var service = NewService(store, out _);

        await service.LoadAsync(CancellationToken.None);

        service.NeedsRefresh().Should().BeFalse();
    }

    [Fact]
    public async Task A_successful_refresh_stores_the_rotated_set()
    {
        var store = new FakeSecretStore().WithTokens("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow));
        var service = NewService(store, out var log);
        await service.LoadAsync(CancellationToken.None);

        await service.RefreshAsync(CancellationToken.None);

        store.TokenWrites.Should().ContainSingle().Which.Should().Be("BMW");
        service.IdToken.Should().Be(RefreshedIdToken);
        log.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The defect behind backlog item 18: a failed Secret write used to be a warning the service
    /// shrugged off, leaving the only valid refresh token in memory with nothing saying so.
    /// </summary>
    [Fact]
    public async Task A_refresh_whose_write_keeps_failing_is_retried_and_then_reported_as_an_error()
    {
        var store = new FailingSecretStore(failures: int.MaxValue)
            .Seed("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow));
        var service = NewService(store, out var log);
        await service.LoadAsync(CancellationToken.None);

        await service.RefreshAsync(CancellationToken.None);

        store.SaveAttempts.Should().Be(4, "a transient write failure deserves more than one try");
        log.Errors.Should().ContainSingle()
            .Which.Should().Contain("NOT saved").And.Contain("pod restart would lose it");

        // The rotated token is still adopted — it is the only one BMW will accept from now on.
        service.IdToken.Should().Be(RefreshedIdToken);
    }

    /// <summary>
    /// The regression test for the defect itself: the rotated set must reach the Secret before
    /// it reaches memory. The other way round, a write failure leaves the Secret holding a token
    /// BMW has already voided while the process quietly carries the only usable one.
    /// </summary>
    [Fact]
    public async Task The_rotated_set_is_persisted_before_it_is_adopted()
    {
        string? inMemoryDuringWrite = null;
        var store = new FakeSecretStore().WithTokens("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow));
        TokenService service = null!;
        store.OnSaveTokens = () => inMemoryDuringWrite = service.IdToken;

        service = NewService(store, out _);
        await service.LoadAsync(CancellationToken.None);
        string beforeRefresh = service.IdToken;

        await service.RefreshAsync(CancellationToken.None);

        inMemoryDuringWrite.Should().Be(beforeRefresh,
            "the new token must not be adopted until the Secret has it");
        service.IdToken.Should().Be(RefreshedIdToken);
    }

    /// <summary>
    /// After an exhausted write the two sides must still be in a relationship the next refresh
    /// can resolve: memory holds the only token BMW still accepts, so the next cycle refreshes
    /// from it and gets another chance to persist. The Secret keeps the voided one — a restart
    /// in that window needs a re-bootstrap, which is exactly what the error line announces.
    /// </summary>
    [Fact]
    public async Task After_an_exhausted_write_memory_holds_the_usable_token_and_the_secret_the_old_one()
    {
        string storedBefore = TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow);
        var store = new FailingSecretStore(failures: int.MaxValue).Seed("BMW", storedBefore);
        var service = NewService(store, out _);
        await service.LoadAsync(CancellationToken.None);

        await service.RefreshAsync(CancellationToken.None);

        service.IdToken.Should().Be(RefreshedIdToken, "only the rotated token is still accepted");
        (await store.GetTokensAsync("BMW")).idToken.Should().Be(storedBefore, "no write got through");

        // And the next cycle retries the write rather than giving up on it.
        store.StopFailing();
        await service.RefreshAsync(CancellationToken.None);
        (await store.GetTokensAsync("BMW")).idToken.Should().Be(RefreshedIdToken);
    }

    [Fact]
    public async Task A_write_that_succeeds_on_a_retry_is_not_reported_as_an_error()
    {
        var store = new FailingSecretStore(failures: 2)
            .Seed("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow));
        var service = NewService(store, out var log);
        await service.LoadAsync(CancellationToken.None);

        await service.RefreshAsync(CancellationToken.None);

        store.SaveAttempts.Should().Be(3);
        log.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_rejected_refresh_token_asks_for_a_re_bootstrap_and_writes_nothing()
    {
        var store = new FakeSecretStore().WithTokens("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow));
        var service = NewService(store, out _, HttpStatusCode.BadRequest);
        await service.LoadAsync(CancellationToken.None);

        var act = async () => await service.RefreshAsync(CancellationToken.None);

        await act.Should().ThrowAsync<BmwAuthExpiredException>();
        store.TokenWrites.Should().BeEmpty();
    }

    private const string RefreshedIdToken = "refreshed.id.token";

    private static TokenService NewService(
        ISecretStore store, out CapturingLogger log, HttpStatusCode status = HttpStatusCode.OK)
    {
        log = new CapturingLogger();
        return new TokenService(Bmw, store, log,
            new StubHandler(status, JsonSerializer.Serialize(new
            {
                id_token      = RefreshedIdToken,
                access_token  = "refreshed-access",
                refresh_token = "refreshed-refresh",
            })),
            persistBackoff: TimeSpan.Zero);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    /// <summary>A store whose token writes fail the first <c>failures</c> times.</summary>
    private sealed class FailingSecretStore(int failures) : ISecretStore
    {
        private readonly FakeSecretStore _inner = new();
        private int _failures = failures;

        public int SaveAttempts { get; private set; }
        public bool RunsInCluster => true;

        public void StopFailing() => _failures = 0;

        public FailingSecretStore Seed(string vehicle, string idToken)
        {
            _inner.WithTokens(vehicle, idToken);
            return this;
        }

        public Task SaveTokensAsync(
            string vehicleName, string idToken, string accessToken, string refreshToken,
            CancellationToken ct = default)
        {
            SaveAttempts++;
            if (SaveAttempts <= _failures)
                throw new InvalidOperationException("Secret is not writable");
            return _inner.SaveTokensAsync(vehicleName, idToken, accessToken, refreshToken, ct);
        }

        public Task<string?> GetCredentialAsync(string key, CancellationToken ct = default)
            => _inner.GetCredentialAsync(key, ct);
        public Task SaveCredentialAsync(string key, string value, CancellationToken ct = default)
            => _inner.SaveCredentialAsync(key, value, ct);
        public Task<bool> HasTokensAsync(string vehicleName, CancellationToken ct = default)
            => _inner.HasTokensAsync(vehicleName, ct);
        public Task<(string idToken, string refreshToken)> GetTokensAsync(
            string vehicleName, CancellationToken ct = default)
            => _inner.GetTokensAsync(vehicleName, ct);
    }

    private sealed class CapturingLogger : ILogger<TokenService>
    {
        public List<string> Errors { get; } = [];

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error) Errors.Add(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
