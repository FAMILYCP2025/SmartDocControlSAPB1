using FluentAssertions;
using SmartDocControl.Application.Models;
using SmartDocControl.Application.Services;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using Xunit;

namespace SmartDocControl.Tests.Application;

public class DocumentCloseEvaluatorTests
{
    private static readonly DateTime BaseDate = new(2026, 1, 1);
    private static readonly DateTime ExecutionDate = new(2026, 3, 1); // 59 days after BaseDate
    private static readonly DateTime FixedProcessedAt = new(2026, 5, 6, 8, 0, 0);

    private static Document CreateDocument() =>
        new(docEntry: 1, cardCode: "C001", documentType: DocumentType.SalesQuotation, baseDate: BaseDate);

    private static DocumentRule CreateEligibleRule() =>
        new("RULE01", DocumentType.SalesQuotation, graceDays: 30, isActive: true);

    private static RunContext CreateContext(bool simulationMode) =>
        new("RUN-001", ExecutionDate, simulationMode, maxDocumentsPerRun: 100, "TST");

    private readonly DocumentCloseEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ShouldReturnSimulated_WhenEligibleAndSimulationModeIsTrue()
    {
        var result = _evaluator.Evaluate(
            CreateDocument(), CreateEligibleRule(), CreateContext(simulationMode: true), FixedProcessedAt);

        result.Status.Should().Be(ExecutionStatus.Simulated);
        result.EligibleForClose.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldReturnPending_WhenEligibleAndSimulationModeIsFalse()
    {
        var result = _evaluator.Evaluate(
            CreateDocument(), CreateEligibleRule(), CreateContext(simulationMode: false), FixedProcessedAt);

        result.Status.Should().Be(ExecutionStatus.Pending);
        result.EligibleForClose.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldReturnSkipped_WhenGracePeriodNotMet()
    {
        var strictRule = new DocumentRule("RULE02", DocumentType.SalesQuotation, graceDays: 90, isActive: true);

        var result = _evaluator.Evaluate(
            CreateDocument(), strictRule, CreateContext(simulationMode: false), FixedProcessedAt);

        result.Status.Should().Be(ExecutionStatus.Skipped);
        result.EligibleForClose.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShouldReturnSkipped_WhenRuleIsInactive()
    {
        var inactiveRule = new DocumentRule("RULE03", DocumentType.SalesQuotation, graceDays: 30, isActive: false);

        var result = _evaluator.Evaluate(
            CreateDocument(), inactiveRule, CreateContext(simulationMode: false), FixedProcessedAt);

        result.Status.Should().Be(ExecutionStatus.Skipped);
        result.EligibleForClose.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShouldUseProvidedProcessedAt_WhenSupplied()
    {
        var result = _evaluator.Evaluate(
            CreateDocument(), CreateEligibleRule(), CreateContext(simulationMode: true), FixedProcessedAt);

        result.ProcessedAt.Should().Be(FixedProcessedAt);
    }

    [Fact]
    public void Evaluate_ShouldAssignProcessedAt_WhenNotSupplied()
    {
        var before = DateTime.UtcNow;
        var result = _evaluator.Evaluate(
            CreateDocument(), CreateEligibleRule(), CreateContext(simulationMode: true));
        var after = DateTime.UtcNow;

        result.ProcessedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Evaluate_ShouldThrow_WhenDocumentIsNull()
    {
        var act = () => _evaluator.Evaluate(null!, CreateEligibleRule(), CreateContext(false));

        act.Should().Throw<ArgumentNullException>().WithParameterName("document");
    }

    [Fact]
    public void Evaluate_ShouldThrow_WhenRuleIsNull()
    {
        var act = () => _evaluator.Evaluate(CreateDocument(), null!, CreateContext(false));

        act.Should().Throw<ArgumentNullException>().WithParameterName("rule");
    }

    [Fact]
    public void Evaluate_ShouldThrow_WhenContextIsNull()
    {
        var act = () => _evaluator.Evaluate(CreateDocument(), CreateEligibleRule(), null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }
}
