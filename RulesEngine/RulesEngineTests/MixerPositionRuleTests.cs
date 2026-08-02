using FluentAssertions;
using RulesEngine.Rules;

namespace RulesEngineTests;

public class MixerPositionRuleTests
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(30);

    private static MixerPositionRule CreateRule(params string[] warmWaterValues)
        => new(warmWaterValues, MaxAge);

    [Fact]
    public void Closes_WhenStatusIsConfiguredWarmWaterValue()
    {
        var rule = CreateRule("4");

        rule.Evaluate("4", Fresh).Should().Be(MixerPosition.Closed);
    }

    [Fact]
    public void Closes_ForAnyOfMultipleConfiguredValues()
    {
        var rule = CreateRule("4", "5");

        rule.Evaluate("4", Fresh).Should().Be(MixerPosition.Closed);
        rule.Evaluate("5", Fresh).Should().Be(MixerPosition.Closed);
    }

    [Fact]
    public void Opens_WhenStatusIsNotInWhitelist()
    {
        var rule = CreateRule("4");

        rule.Evaluate("2", Fresh).Should().Be(MixerPosition.Open);
    }

    [Fact]
    public void Opens_ForUnknownStatusValue()
    {
        var rule = CreateRule("4");

        rule.Evaluate("99", Fresh).Should().Be(MixerPosition.Open);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Opens_WhenStatusIsMissing(string? faStatus)
    {
        var rule = CreateRule("4");

        rule.Evaluate(faStatus, Fresh).Should().Be(MixerPosition.Open);
    }

    [Fact]
    public void Opens_WhenWhitelistIsEmpty()
    {
        var rule = CreateRule();

        rule.Evaluate("4", Fresh).Should().Be(MixerPosition.Open);
    }

    [Fact]
    public void Opens_WhenStatusIsStale()
    {
        var rule = CreateRule("4");

        rule.Evaluate("4", MaxAge + TimeSpan.FromSeconds(1)).Should().Be(MixerPosition.Open);
    }

    [Fact]
    public void Closes_WhenStatusAgeIsExactlyAtLimit()
    {
        var rule = CreateRule("4");

        rule.Evaluate("4", MaxAge).Should().Be(MixerPosition.Closed);
    }

    [Fact]
    public void Opens_WhenStatusWasNeverReceived()
    {
        var rule = CreateRule("4");

        rule.Evaluate(null, TimeSpan.MaxValue).Should().Be(MixerPosition.Open);
    }

    [Fact]
    public void TrimsWhitespaceAroundStatusValue()
    {
        var rule = CreateRule("4");

        rule.Evaluate(" 4 \n", Fresh).Should().Be(MixerPosition.Closed);
    }

    [Fact]
    public void TrimsWhitespaceAroundConfiguredValues()
    {
        var rule = new MixerPositionRule(new[] { " 4 ", "", "  " }, MaxAge);

        rule.Evaluate("4", Fresh).Should().Be(MixerPosition.Closed);
        rule.Evaluate("", Fresh).Should().Be(MixerPosition.Open);
    }
}
