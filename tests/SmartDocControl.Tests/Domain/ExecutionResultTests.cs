using FluentAssertions;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using Xunit;

namespace SmartDocControl.Tests.Domain;

public class ExecutionResultTests
{
    private static Document CreateDocument() =>
        new(docEntry: 1, cardCode: "C001", documentType: DocumentType.SalesQuotation,
            baseDate: new DateTime(2026, 1, 1));

    [Fact]
    public void Constructor_ShouldCreateResult_WhenValidParameters()
    {
        var doc = CreateDocument();
        var executedAt = new DateTime(2026, 5, 6, 8, 0, 0);

        var result = new ExecutionResult(doc, CloseDecision.Eligible, ExecutionStatus.Processed, "Closed.", executedAt);

        result.Document.Should().BeSameAs(doc);
        result.Decision.Should().Be(CloseDecision.Eligible);
        result.Status.Should().Be(ExecutionStatus.Processed);
        result.Message.Should().Be("Closed.");
        result.ExecutedAt.Should().Be(executedAt);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDocumentIsNull()
    {
        var act = () => new ExecutionResult(null!, CloseDecision.Eligible, ExecutionStatus.Processed,
            "msg", DateTime.UtcNow);

        act.Should().Throw<ArgumentNullException>().WithParameterName("document");
    }

    [Fact]
    public void Constructor_ShouldUseEmptyString_WhenMessageIsNull()
    {
        var doc = CreateDocument();

        var result = new ExecutionResult(doc, CloseDecision.Eligible, ExecutionStatus.Processed,
            null!, DateTime.UtcNow);

        result.Message.Should().Be(string.Empty);
    }
}
