using FluentAssertions;
using SmartDocControl.Domain.Enums;
using SmartDocControl.Domain.ValueObjects;
using Xunit;

namespace SmartDocControl.Tests.Domain;

public class RuleEvaluationResultTests
{
    [Fact]
    public void RuleEvaluationResult_ShouldBeImmutable_WhenCreated()
    {
        var result = new RuleEvaluationResult(CloseDecision.Eligible, "Eligible for closing.", 45);

        result.Decision.Should().Be(CloseDecision.Eligible);
        result.Reason.Should().Be("Eligible for closing.");
        result.EvaluatedDays.Should().Be(45);
    }

    [Fact]
    public void RuleEvaluationResult_ShouldBeEqual_WhenSameValues()
    {
        var a = new RuleEvaluationResult(CloseDecision.Eligible, "Eligible for closing.", 45);
        var b = new RuleEvaluationResult(CloseDecision.Eligible, "Eligible for closing.", 45);

        a.Should().Be(b);
    }
}
