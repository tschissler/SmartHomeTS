using BMWConnector.Models;
using FluentAssertions;

namespace BMWConnectorTests;

/// <summary>
/// End-to-end over the credential loading path, with the Kubernetes Secret faked.
/// Each test uses its own vehicle prefix so the process-wide environment variables
/// it sets cannot collide with a test running in parallel.
/// </summary>
public class VehicleConfigTests : IDisposable
{
    private const string SecretGcid     = "11111111-2222-3333-4444-555555555555";
    private const string SecretClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string OtherUuid      = "99999999-8888-7777-6666-555555555555";

    private readonly List<string> _touchedVariables = [];

    private void SetEnv(string key, string? value)
    {
        _touchedVariables.Add(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    public void Dispose()
    {
        foreach (var key in _touchedVariables)
            Environment.SetEnvironmentVariable(key, null);
    }

    private static FakeSecretStore StoreFor(string prefix, bool inCluster) =>
        new FakeSecretStore { RunsInCluster = inCluster }
            .WithCredential($"{prefix}_GCID", SecretGcid)
            .WithCredential($"{prefix}_CLIENT_ID", SecretClientId);

    /// <summary>
    /// The completion criterion for backlog item 18: a start with the exact environment that
    /// destroyed the Secret on 2026-09-20 must leave it untouched.
    /// </summary>
    [Fact]
    public async Task Placeholder_env_vars_do_not_change_the_secret()
    {
        const string prefix = "TestPlaceholder";
        SetEnv($"{prefix}_GCID", "REPLACE_ME");
        SetEnv($"{prefix}_CLIENT_ID", "REPLACE_ME");
        var store = StoreFor(prefix, inCluster: false);

        var config = await VehicleConfig.CreateAsync(prefix, store, allowCredentialWriteBack: true);

        store.CredentialWrites.Should().BeEmpty("a placeholder must never reach the Secret");
        config.Gcid.Should().Be(SecretGcid);
        config.ClientId.Should().Be(SecretClientId);
    }

    [Fact]
    public async Task Malformed_env_vars_do_not_change_the_secret()
    {
        const string prefix = "TestMalformed";
        SetEnv($"{prefix}_GCID", "nonsense");
        SetEnv($"{prefix}_CLIENT_ID", "123");
        var store = StoreFor(prefix, inCluster: false);

        var config = await VehicleConfig.CreateAsync(prefix, store, allowCredentialWriteBack: true);

        store.CredentialWrites.Should().BeEmpty();
        config.Gcid.Should().Be(SecretGcid);
    }

    [Fact]
    public async Task In_the_cluster_a_well_formed_env_var_is_ignored_and_nothing_is_written()
    {
        const string prefix = "TestInCluster";
        SetEnv($"{prefix}_GCID", OtherUuid);
        var store = StoreFor(prefix, inCluster: true);

        var config = await VehicleConfig.CreateAsync(prefix, store, allowCredentialWriteBack: true);

        store.CredentialWrites.Should().BeEmpty();
        config.Gcid.Should().Be(SecretGcid);
    }

    [Fact]
    public async Task Locally_a_well_formed_env_var_overrides_without_writing_back()
    {
        const string prefix = "TestLocalOverride";
        SetEnv($"{prefix}_GCID", OtherUuid);
        var store = StoreFor(prefix, inCluster: false);

        var config = await VehicleConfig.CreateAsync(prefix, store, allowCredentialWriteBack: false);

        store.CredentialWrites.Should().BeEmpty();
        config.Gcid.Should().Be(OtherUuid);
    }

    [Fact]
    public async Task With_the_opt_in_a_well_formed_env_var_is_written_back()
    {
        const string prefix = "TestOptIn";
        SetEnv($"{prefix}_GCID", OtherUuid);
        var store = StoreFor(prefix, inCluster: false);

        await VehicleConfig.CreateAsync(prefix, store, allowCredentialWriteBack: true);

        store.CredentialWrites.Should().ContainSingle()
            .Which.Should().Be(($"{prefix}_GCID", OtherUuid));
    }

    [Fact]
    public async Task A_credential_missing_everywhere_reports_where_to_get_it()
    {
        const string prefix = "TestMissing";
        SetEnv($"{prefix}_GCID", null);
        var store = new FakeSecretStore { RunsInCluster = true };

        var act = async () => await VehicleConfig.CreateAsync(prefix, store);

        (await act.Should().ThrowAsync<MissingCredentialException>())
            .Which.Message.Should().Contain("bmwconnector-credentials");
        store.CredentialWrites.Should().BeEmpty();
    }

    /// <summary>
    /// The default topic follows Docs/MQTT-Topic-Konvention.md. Asserted as a literal rather
    /// than against <see cref="SharedContracts.FahrzeugTopics"/>, so that the test fails if the
    /// spelling of the topic changes anywhere — comparing the code against itself would pass
    /// whatever either side said.
    /// </summary>
    [Fact]
    public async Task The_output_topic_falls_back_to_the_conventional_one()
    {
        const string prefix = "TestTopic";
        SetEnv($"{prefix}_OUTPUT_TOPIC", null);
        var store = StoreFor(prefix, inCluster: true);

        var config = await VehicleConfig.CreateAsync(prefix, store);

        config.OutputTopic.Should().Be($"daten/Fahrzeug/{prefix}/Status");
    }

    /// <summary>
    /// The trap this point had to walk around: the topic is overridable, so changing the
    /// default in code is not enough while the deployment sets the variable. The override still
    /// works — it is a development aid — but the test pins that it does, so nobody assumes the
    /// code default is the whole story.
    /// </summary>
    [Fact]
    public async Task An_environment_variable_still_overrides_the_output_topic()
    {
        const string prefix = "TestTopicOverride";
        SetEnv($"{prefix}_OUTPUT_TOPIC", "daten/Fahrzeug/Testwagen/Status");
        var store = StoreFor(prefix, inCluster: true);

        var config = await VehicleConfig.CreateAsync(prefix, store);

        config.OutputTopic.Should().Be("daten/Fahrzeug/Testwagen/Status");
    }
}
