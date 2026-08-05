using FluentAssertions;
using RulesEngine.Rules;

namespace RulesEngineTests;

public class CoolingFlowTemperatureRuleTests
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Stale = TimeSpan.FromMinutes(16);

    private static CoolingFlowTemperatureRule CreateRule(
        double deadband = 0.5,
        double pulseSecondsPerKelvin = 5.0,
        int minPulseSeconds = 2,
        int maxPulseSeconds = 20)
        => new(["2"], MaxAge, deadband, pulseSecondsPerKelvin, minPulseSeconds, maxPulseSeconds);

    private static MixerPulse? Evaluate(
        CoolingFlowTemperatureRule rule,
        MixerPosition? basePosition = MixerPosition.Open,
        string? faStatus = "2",
        TimeSpan? faStatusAge = null,
        bool? pumpRunning = true,
        TimeSpan? pumpAge = null,
        double? flowTemperature = 15.0,
        TimeSpan? flowAge = null,
        double target = 15.0)
        => rule.Evaluate(
            basePosition,
            faStatus, faStatusAge ?? Fresh,
            pumpRunning, pumpAge ?? Fresh,
            flowTemperature, flowAge ?? Fresh,
            target);

    [Fact]
    public void DoesNothing_WhenTemperatureIsWithinDeadband()
    {
        var rule = CreateRule();

        Evaluate(rule, flowTemperature: 15.0).Should().BeNull();
        Evaluate(rule, flowTemperature: 15.4).Should().BeNull();
        Evaluate(rule, flowTemperature: 14.6).Should().BeNull();
    }

    [Fact]
    public void PulsesTowardsClose_WhenFlowIsTooCold()
    {
        var rule = CreateRule();

        var pulse = Evaluate(rule, flowTemperature: 13.0);

        pulse.Should().NotBeNull();
        pulse!.Direction.Should().Be(PulseDirection.Close);
        pulse.Seconds.Should().Be(10); // 2 K unter Soll * 5 s/K
    }

    [Fact]
    public void PulsesTowardsOpen_WhenFlowIsTooWarm()
    {
        var rule = CreateRule();

        var pulse = Evaluate(rule, flowTemperature: 16.5);

        pulse.Should().NotBeNull();
        pulse!.Direction.Should().Be(PulseDirection.Open);
        pulse.Seconds.Should().Be(8); // 1,5 K über Soll * 5 s/K
    }

    [Fact]
    public void ClampsPulseToMaximum()
    {
        var rule = CreateRule();

        var pulse = Evaluate(rule, flowTemperature: 5.0);

        pulse!.Seconds.Should().Be(20);
    }

    [Fact]
    public void ClampsPulseToMinimum()
    {
        var rule = CreateRule(deadband: 0.1, pulseSecondsPerKelvin: 1.0);

        var pulse = Evaluate(rule, flowTemperature: 14.5);

        pulse!.Seconds.Should().Be(2);
    }

    [Fact]
    public void DoesNothing_WhenBaseRuleDoesNotSayOpen()
    {
        var rule = CreateRule();

        // Warm water preparation (base rule "close") has priority over trimming
        Evaluate(rule, basePosition: MixerPosition.Closed, flowTemperature: 13.0).Should().BeNull();
        Evaluate(rule, basePosition: null, flowTemperature: 13.0).Should().BeNull();
    }

    [Fact]
    public void DoesNothing_WhenStatusIsNotACoolingValue()
    {
        var rule = CreateRule();

        Evaluate(rule, faStatus: "0", flowTemperature: 13.0).Should().BeNull();
        Evaluate(rule, faStatus: "4", flowTemperature: 13.0).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DoesNothing_WhenStatusIsMissing(string? faStatus)
    {
        var rule = CreateRule();

        Evaluate(rule, faStatus: faStatus, flowTemperature: 13.0).Should().BeNull();
    }

    [Fact]
    public void DoesNothing_WhenStatusIsStale()
    {
        var rule = CreateRule();

        Evaluate(rule, faStatusAge: Stale, flowTemperature: 13.0).Should().BeNull();
    }

    [Fact]
    public void DoesNothing_WhenPumpIsOff()
    {
        var rule = CreateRule();

        Evaluate(rule, pumpRunning: false, flowTemperature: 13.0).Should().BeNull();
    }

    [Fact]
    public void DoesNothing_WhenPumpStatusIsMissingOrStale()
    {
        var rule = CreateRule();

        Evaluate(rule, pumpRunning: null, flowTemperature: 13.0).Should().BeNull();
        Evaluate(rule, pumpAge: Stale, flowTemperature: 13.0).Should().BeNull();
    }

    [Fact]
    public void DoesNothing_WhenFlowTemperatureIsMissingOrStale()
    {
        var rule = CreateRule();

        Evaluate(rule, flowTemperature: null).Should().BeNull();
        Evaluate(rule, flowTemperature: 13.0, flowAge: Stale).Should().BeNull();
    }

    [Fact]
    public void TrimsWhitespaceAroundStatusValue()
    {
        var rule = CreateRule();

        Evaluate(rule, faStatus: " 2 \n", flowTemperature: 13.0).Should().NotBeNull();
    }

    [Fact]
    public void SupportsMultipleCoolingStatusValues()
    {
        var rule = new CoolingFlowTemperatureRule(["2", "8"], MaxAge, 0.5, 5.0, 2, 20);

        Evaluate(rule, faStatus: "8", flowTemperature: 13.0).Should().NotBeNull();
    }

    [Fact]
    public void UsesTargetTemperaturePassedPerEvaluation()
    {
        var rule = CreateRule();

        // 15.2°C flow: within deadband for target 15, but 2.8 K too cold for target 18
        Evaluate(rule, flowTemperature: 15.2, target: 15.0).Should().BeNull();

        var pulse = Evaluate(rule, flowTemperature: 15.2, target: 18.0);
        pulse!.Direction.Should().Be(PulseDirection.Close);
        pulse.Seconds.Should().Be(14); // 2,8 K * 5 s/K

    }
}
