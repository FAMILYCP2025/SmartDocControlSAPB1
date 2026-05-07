using FluentAssertions;
using SmartDocControl.Application.Models;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using SmartDocControl.Domain.ValueObjects;
using Xunit;

namespace SmartDocControl.Tests.Application;

public class DocumentProcessingResultTests
{
    private static readonly DateTime FixedAt = new(2026, 5, 6, 8, 0, 0);

    private static Document CreateDocument() =>
        new(docEntry: 1, cardCode: "C001", documentType: DocumentType.SalesQuotation,
            baseDate: new DateTime(2026, 1, 1));

    private static RuleEvaluationResult CreateEvaluation(CloseDecision decision = CloseDecision.Eligible) =>
        new(decision, "Test reason.", 30);

    [Fact]
    public void Constructor_ShouldCreate_WhenValidParameters()
    {
        var doc = CreateDocument();
        var eval = CreateEvaluation();

        var result = new DocumentProcessingResult(
            doc, eval, ExecutionStatus.Simulated, "Simulated.", eligibleForClose: true, FixedAt);

        result.Document.Should().BeSameAs(doc);
        result.EvaluationResult.Should().Be(eval);
        result.Status.Should().Be(ExecutionStatus.Simulated);
        result.Message.Should().Be("Simulated.");
        result.EligibleForClose.Should().BeTrue();
        result.ProcessedAt.Should().Be(FixedAt);
    }

    [Fact]
    public void Constructor_ShouldUseEmptyString_WhenMessageIsNull()
    {
        var result = new DocumentProcessingResult(
            CreateDocument(), CreateEvaluation(), ExecutionStatus.Skipped, null!, false, FixedAt);

        result.Message.Should().Be(string.Empty);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDocumentIsNull()
    {
        var act = () => new DocumentProcessingResult(
            null!, CreateEvaluation(), ExecutionStatus.Skipped, "msg", false, FixedAt);

        act.Should().Throw<ArgumentNullException>().WithParameterName("document");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEvaluationResultIsNull()
    {
        var act = () => new DocumentProcessingResult(
            CreateDocument(), null!, ExecutionStatus.Skipped, "msg", false, FixedAt);

        act.Should().Throw<ArgumentNullException>().WithParameterName("evaluationResult");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EligibleForClose_ShouldReflectProvidedValue(bool eligible)
    {
        var result = new DocumentProcessingResult(
            CreateDocument(), CreateEvaluation(), ExecutionStatus.Pending, "msg", eligible, FixedAt);

        result.EligibleForClose.Should().Be(eligible);
    }
}
