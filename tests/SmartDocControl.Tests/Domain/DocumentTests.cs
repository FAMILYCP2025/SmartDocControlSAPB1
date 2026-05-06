using FluentAssertions;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using Xunit;

namespace SmartDocControl.Tests.Domain;

public class DocumentTests
{
    private static Document CreateValid() =>
        new(docEntry: 1, cardCode: "C001", documentType: DocumentType.SalesQuotation,
            baseDate: new DateTime(2026, 1, 1));

    [Fact]
    public void Constructor_ShouldCreateDocument_WhenValidParameters()
    {
        var doc = CreateValid();

        doc.DocEntry.Should().Be(1);
        doc.CardCode.Should().Be("C001");
        doc.DocumentType.Should().Be(DocumentType.SalesQuotation);
        doc.BaseDate.Should().Be(new DateTime(2026, 1, 1));
        doc.IsClosed.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrow_WhenDocEntryIsZeroOrNegative(int docEntry)
    {
        var act = () => new Document(docEntry, "C001", DocumentType.SalesQuotation, new DateTime(2026, 1, 1));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("docEntry");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenCardCodeIsEmpty(string cardCode)
    {
        var act = () => new Document(1, cardCode, DocumentType.SalesQuotation, new DateTime(2026, 1, 1));

        act.Should().Throw<ArgumentException>().WithParameterName("cardCode");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenBaseDateIsDefault()
    {
        var act = () => new Document(1, "C001", DocumentType.SalesQuotation, default);

        act.Should().Throw<ArgumentException>().WithParameterName("baseDate");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDocumentTypeIsInvalid()
    {
        var act = () => new Document(1, "C001", (DocumentType)99, new DateTime(2026, 1, 1));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("documentType");
    }

    [Fact]
    public void MarkAsClosed_ShouldSetIsClosedToTrue()
    {
        var doc = CreateValid();

        doc.MarkAsClosed();

        doc.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void CanBeClosed_ShouldReturnFalse_WhenDocumentIsAlreadyClosed()
    {
        var doc = CreateValid();
        doc.MarkAsClosed();

        doc.CanBeClosed().Should().BeFalse();
    }
}
