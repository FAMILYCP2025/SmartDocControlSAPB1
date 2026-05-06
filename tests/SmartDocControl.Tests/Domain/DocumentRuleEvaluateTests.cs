using FluentAssertions;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using Xunit;

namespace SmartDocControl.Tests.Domain;

public class DocumentRuleEvaluateTests
{
    private static readonly DateTime BaseDate = new(2026, 1, 1);
    private static readonly DateTime ExecutionDate = new(2026, 2, 15); // 45 days after BaseDate

    private static Document CreateDocument(
        bool hasTargetDocument = false,
        bool hasRecentActivity = false,
        bool isClosed = false) =>
        new(docEntry: 1, cardCode: "C001", documentType: DocumentType.SalesQuotation,
            baseDate: BaseDate, hasTargetDocument: hasTargetDocument,
            hasRecentActivity: hasRecentActivity, isClosed: isClosed);

    private static DocumentRule CreateRule(
        bool isActive = true,
        int graceDays = 30,
        bool requireNoTarget = false,
        bool requireNoRecentActivity = false,
        bool requireApproved = false) =>
        new("RULE01", DocumentType.SalesQuotation, graceDays, isActive,
            requireNoTarget: requireNoTarget,
            requireNoRecentActivity: requireNoRecentActivity,
            requireApproved: requireApproved);

    [Fact]
    public void Evaluate_ShouldReturnSkipInactiveRule_WhenRuleIsInactive()
    {
        var result = CreateRule(isActive: false).Evaluate(CreateDocument(), ExecutionDate);

        result.Decision.Should().Be(CloseDecision.SkipInactiveRule);
    }

    [Fact]
    public void Evaluate_ShouldReturnSkipClosed_WhenDocumentIsClosed()
    {
        var result = CreateRule().Evaluate(CreateDocument(isClosed: true), ExecutionDate);

        result.Decision.Should().Be(CloseDecision.SkipClosed);
    }

    [Fact]
    public void Evaluate_ShouldReturnSkipApprovalRequired_WhenRequireApprovedIsTrue()
    {
        var result = CreateRule(requireApproved: true).Evaluate(CreateDocument(), ExecutionDate);

        result.Decision.Should().Be(CloseDecision.SkipApprovalRequired);
    }

    [Fact]
    public void Evaluate_ShouldReturnSkipHasTarget_WhenRequireNoTargetAndDocumentHasTarget()
    {
        var result = CreateRule(requireNoTarget: true)
            .Evaluate(CreateDocument(hasTargetDocument: true), ExecutionDate);

        result.Decision.Should().Be(CloseDecision.SkipHasTarget);
    }

    [Fact]
    public void Evaluate_ShouldReturnSkipRecentActivity_WhenRequireNoRecentActivityAndDocumentHasActivity()
    {
        var result = CreateRule(requireNoRecentActivity: true)
            .Evaluate(CreateDocument(hasRecentActivity: true), ExecutionDate);

        result.Decision.Should().Be(CloseDecision.SkipRecentActivity);
    }

    [Fact]
    public void Evaluate_ShouldReturnSkipGracePeriod_WhenElapsedDaysLessThanGraceDays()
    {
        var result = CreateRule(graceDays: 60).Evaluate(CreateDocument(), ExecutionDate);

        result.Decision.Should().Be(CloseDecision.SkipGracePeriod);
        result.EvaluatedDays.Should().Be(45);
    }

    [Theory]
    [InlineData(30)]  // exactly at boundary
    [InlineData(20)]  // well past grace period
    public void Evaluate_ShouldReturnEligible_WhenElapsedDaysEqualOrGreaterThanGraceDays(int graceDays)
    {
        var result = CreateRule(graceDays: graceDays).Evaluate(CreateDocument(), ExecutionDate);

        result.Decision.Should().Be(CloseDecision.Eligible);
        result.EvaluatedDays.Should().Be(45);
    }
}
