using System.Text.Json;
using FluentAssertions;
using HeartbeatLib;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HeartbeatLibTests;

public class ServiceHeartbeatTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private static HealthReport Report(HealthStatus status, params (string Name, string? Description)[] entries)
    {
        var dict = entries.ToDictionary(
            e => e.Name,
            e => new HealthReportEntry(status, e.Description, TimeSpan.Zero, exception: null, data: null));
        return new HealthReport(dict, TimeSpan.Zero);
    }

    private static JsonElement Parse(string payload) => JsonDocument.Parse(payload).RootElement;

    [Fact]
    public void Topic_follows_the_existing_device_format_with_Cluster_as_the_location()
    {
        var heartbeat = new ServiceHeartbeat("KebaConnector", "1.0.42");

        heartbeat.Topic.Should().Be("status/Cluster/Dienst/KebaConnector");
    }

    [Theory]
    [InlineData("Keba/Connector")]
    [InlineData("Keba+Connector")]
    [InlineData("Keba#Connector")]
    [InlineData("  ")]
    public void A_name_that_is_not_a_topic_level_is_rejected_instead_of_producing_a_wrong_topic(string name)
    {
        var bauen = () => new ServiceHeartbeat(name, "1.0.42");

        bauen.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Payload_carries_the_identity_the_device_page_groups_and_names_cards_by()
    {
        var heartbeat = new ServiceHeartbeat("ChargingController", "1.0.482", () => Start);

        var payload = Parse(heartbeat.BuildPayload(Report(HealthStatus.Healthy, ("x", "fine"))));

        payload.GetProperty("location").GetString().Should().Be("Cluster");
        payload.GetProperty("deviceType").GetString().Should().Be("Dienst");
        payload.GetProperty("deviceName").GetString().Should().Be("ChargingController");
        payload.GetProperty("version").GetString().Should().Be("1.0.482");
    }

    [Fact]
    public void Zeitpunkt_is_the_moment_of_publication_in_UTC()
    {
        var jetzt = Start;
        var heartbeat = new ServiceHeartbeat("DataHub", "1.0.1", () => jetzt);
        jetzt = Start.AddMinutes(90);

        var payload = Parse(heartbeat.BuildPayload(Report(HealthStatus.Healthy)));

        payload.GetProperty("Zeitpunkt").GetDateTimeOffset().Should().Be(Start.AddMinutes(90));
        payload.GetProperty("uptimeSeconds").GetInt64().Should().Be(90 * 60);
    }

    [Fact]
    public void Zeitpunkt_stays_UTC_even_when_the_clock_hands_out_a_local_offset()
    {
        // The whole point of the field is that a consumer can subtract it from its own UTC now.
        var mitOffset = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(2));
        var heartbeat = new ServiceHeartbeat("DataHub", "1.0.1", () => mitOffset);

        var payload = Parse(heartbeat.BuildPayload(Report(HealthStatus.Healthy)));

        payload.GetProperty("Zeitpunkt").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);
        payload.GetProperty("Zeitpunkt").GetDateTimeOffset().Should().Be(mitOffset);
    }

    [Fact]
    public void Hardware_fields_of_the_device_format_are_absent_rather_than_zero()
    {
        var heartbeat = new ServiceHeartbeat("VWConnector", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(Report(HealthStatus.Healthy)));

        foreach (var feld in new[] { "mac", "chipModel", "ip", "rssi", "freeHeap", "resetReason", "mqttConnects" })
        {
            payload.TryGetProperty(feld, out _).Should().BeFalse(
                $"a pod has no honest value for {feld}, and a 0 would be read as a measurement");
        }
    }

    [Fact]
    public void Without_a_last_successful_action_the_field_is_left_out()
    {
        var heartbeat = new ServiceHeartbeat("BMWConnector", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(Report(HealthStatus.Healthy)));

        payload.TryGetProperty("lastDataSecondsAgo", out _).Should().BeFalse();
    }

    [Fact]
    public void The_last_successful_action_is_reported_as_an_age_like_the_devices_do()
    {
        var heartbeat = new ServiceHeartbeat("BMWConnector", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(
            Report(HealthStatus.Healthy), letzteAktion: Start.AddSeconds(-42)));

        payload.GetProperty("lastDataSecondsAgo").GetInt64().Should().Be(42);
    }

    [Fact]
    public void A_clock_skew_never_produces_a_negative_age()
    {
        var heartbeat = new ServiceHeartbeat("BMWConnector", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(
            Report(HealthStatus.Healthy), letzteAktion: Start.AddSeconds(5)));

        payload.GetProperty("lastDataSecondsAgo").GetInt64().Should().Be(0);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, "Healthy")]
    [InlineData(HealthStatus.Degraded, "Degraded")]
    [InlineData(HealthStatus.Unhealthy, "Unhealthy")]
    public void Zustand_is_the_status_of_the_services_own_health_checks(HealthStatus status, string erwartet)
    {
        var heartbeat = new ServiceHeartbeat("KebaConnector", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(Report(status, ("keba", "whatever"))));

        payload.GetProperty("Zustand").GetString().Should().Be(erwartet);
    }

    [Fact]
    public void A_single_check_contributes_its_description_unprefixed()
    {
        var heartbeat = new ServiceHeartbeat("KebaConnector", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(
            Report(HealthStatus.Healthy, ("keba_connector", "Last successful read: 4 seconds ago"))));

        payload.GetProperty("ZustandText").GetString().Should().Be("Last successful read: 4 seconds ago");
    }

    [Fact]
    public void Several_checks_are_named_so_it_stays_clear_which_one_complains()
    {
        var heartbeat = new ServiceHeartbeat("DataHub", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(Report(HealthStatus.Degraded,
            ("data-pipeline", "no write for 4.0 minutes"),
            ("process-liveness", "serving"))));

        payload.GetProperty("ZustandText").GetString()
            .Should().Be("data-pipeline: no write for 4.0 minutes | process-liveness: serving");
    }

    [Fact]
    public void A_check_without_a_description_leaves_the_text_out_entirely()
    {
        var heartbeat = new ServiceHeartbeat("DataHub", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(Report(HealthStatus.Healthy, ("data-pipeline", null))));

        payload.TryGetProperty("ZustandText", out _).Should().BeFalse();
    }

    [Fact]
    public void An_exception_stands_in_for_the_missing_description_but_is_truncated()
    {
        var heartbeat = new ServiceHeartbeat("DataHub", "1.0.1", () => Start);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["pipe"] = new(HealthStatus.Unhealthy, description: null, TimeSpan.Zero,
                    new InvalidOperationException(new string('x', 900)), data: null),
            },
            TimeSpan.Zero);

        var text = Parse(heartbeat.BuildPayload(report)).GetProperty("ZustandText").GetString();

        // Retained payloads stay on the broker until overwritten; a stack-trace-sized one would
        // be handed to every subscriber on every connect.
        text.Should().HaveLength(501).And.EndWith("…");
    }

    [Fact]
    public void A_single_check_result_can_be_used_where_there_is_no_report()
    {
        var heartbeat = new ServiceHeartbeat("BMWConnector", "1.0.1", () => Start);

        var payload = Parse(heartbeat.BuildPayload(
            HealthCheckResult.Degraded("Partial outage — connected: Mini")));

        payload.GetProperty("Zustand").GetString().Should().Be("Degraded");
        payload.GetProperty("ZustandText").GetString().Should().Be("Partial outage — connected: Mini");
    }
}
