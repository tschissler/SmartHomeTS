using BMWConnector.Services;
using FluentAssertions;

namespace BMWConnectorTests;

/// <summary>
/// The rules that keep an environment variable from reaching the production Secret.
/// Motivated by the 2026-09-20 incident: placeholders inherited from the user's systemd
/// environment overwrote CLIENT_ID and GCID, which existed nowhere else.
/// </summary>
public class CredentialResolverTests
{
    private const string SecretUuid = "11111111-2222-3333-4444-555555555555";
    private const string EnvUuid    = "99999999-8888-7777-6666-555555555555";

    [Theory]
    [InlineData("REPLACE_ME")]
    [InlineData("replace_me")]
    [InlineData("CHANGEME")]
    [InlineData("your-bmw-client-id-here")]
    [InlineData("<your BMW account UUID>")]
    [InlineData("TODO")]
    public void Placeholder_env_value_is_refused_and_the_secret_is_used(string placeholder)
    {
        var decision = CredentialResolver.Resolve(
            "BMW_GCID", placeholder, SecretUuid, runningInCluster: false, writeBackOptIn: true);

        decision.Action.Should().Be(CredentialAction.UseSecret);
        decision.Value.Should().Be(SecretUuid);
        decision.Warnings.Should().ContainSingle()
            .Which.Should().Contain("unfilled template placeholder");
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("1111111122223333444455555555555")]      // right length-ish, no dashes
    [InlineData("11111111-2222-3333-4444-5555555555")]    // too short
    public void Malformed_env_value_is_refused_and_the_secret_is_used(string malformed)
    {
        var decision = CredentialResolver.Resolve(
            "BMW_CLIENT_ID", malformed, SecretUuid, runningInCluster: false, writeBackOptIn: true);

        decision.Action.Should().Be(CredentialAction.UseSecret);
        decision.Value.Should().Be(SecretUuid);
        decision.Warnings.Should().ContainSingle().Which.Should().Contain("UUIDs");
    }

    [Fact]
    public void In_the_cluster_the_secret_beats_even_a_well_formed_env_value()
    {
        var decision = CredentialResolver.Resolve(
            "BMW_GCID", EnvUuid, SecretUuid, runningInCluster: true, writeBackOptIn: true);

        decision.Action.Should().Be(CredentialAction.UseSecret);
        decision.Value.Should().Be(SecretUuid);
        decision.Warnings.Should().ContainSingle().Which.Should().Contain("authoritative");
    }

    [Fact]
    public void Locally_a_well_formed_env_value_overrides_but_does_not_write_back()
    {
        var decision = CredentialResolver.Resolve(
            "BMW_GCID", EnvUuid, SecretUuid, runningInCluster: false, writeBackOptIn: false);

        decision.Action.Should().Be(CredentialAction.UseEnvironment);
        decision.Value.Should().Be(EnvUuid);
        decision.Warnings.Should().ContainSingle().Which.Should().Contain("Secret is left unchanged");
    }

    [Fact]
    public void Write_back_happens_only_on_explicit_opt_in()
    {
        var decision = CredentialResolver.Resolve(
            "BMW_GCID", EnvUuid, SecretUuid, runningInCluster: false, writeBackOptIn: true);

        decision.Action.Should().Be(CredentialAction.UseEnvironmentAndSave);
        decision.Value.Should().Be(EnvUuid);
    }

    [Fact]
    public void Without_any_source_the_credential_counts_as_missing()
    {
        var decision = CredentialResolver.Resolve(
            "BMW_GCID", null, null, runningInCluster: true, writeBackOptIn: false);

        decision.Action.Should().Be(CredentialAction.Missing);
        decision.Value.Should().BeEmpty();
    }

    [Fact]
    public void An_env_value_matching_the_secret_needs_no_override_warning()
    {
        var decision = CredentialResolver.Resolve(
            "BMW_GCID", SecretUuid, SecretUuid, runningInCluster: false, writeBackOptIn: false);

        decision.Action.Should().Be(CredentialAction.UseEnvironment);
        decision.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Describe_masks_a_real_value_but_states_its_length()
    {
        string described = CredentialResolver.Describe(SecretUuid);

        described.Should().NotContain("2222-3333-4444");
        described.Should().Be("1111…5555 (36 bytes)");
    }

    [Fact]
    public void Describe_names_a_recognised_placeholder_so_its_source_can_be_found()
    {
        // A placeholder holds no secret, and naming it is the only way an operator
        // learns to look in `systemctl --user show-environment`.
        CredentialResolver.Describe("REPLACE_ME").Should().Be("'REPLACE_ME'");
    }
}
