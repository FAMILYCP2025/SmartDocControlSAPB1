using FluentAssertions;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using Xunit;

namespace SmartDocControl.Tests.Domain;

public class DocumentRuleTests
{
    [Fact]
    public void Constructor_ShouldCreateRule_WhenValidParameters()
    {
        var rule = new DocumentRule("RULE01", DocumentType.SalesOrder, graceDays: 30, isActive: true);

        rule.RuleCode.Should().Be("RULE01");
        rule.DocumentType.Should().Be(DocumentType.SalesOrder);
        rule.GraceDays.Should().Be(30);
        rule.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenRuleCodeIsEmpty(string ruleCode)
    {
        var act = () => new DocumentRule(ruleCode, DocumentType.SalesOrder, graceDays: 30, isActive: true);

        act.Should().Throw<ArgumentException>().WithParameterName("ruleCode");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenGraceDaysIsNegative()
    {
        var act = () => new DocumentRule("RULE01", DocumentType.SalesOrder, graceDays: -1, isActive: true);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("graceDays");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDocumentTypeIsInvalid()
    {
        var act = () => new DocumentRule("RULE01", (DocumentType)99, graceDays: 30, isActive: true);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("documentType");
    }
}
