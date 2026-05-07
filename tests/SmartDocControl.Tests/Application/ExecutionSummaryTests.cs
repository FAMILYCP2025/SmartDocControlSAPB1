using FluentAssertions;
using SmartDocControl.Application.Models;
using Xunit;

namespace SmartDocControl.Tests.Application;

public class ExecutionSummaryTests
{
    private static readonly DateTime ValidStart = new(2026, 5, 6, 8, 0, 0);

    [Fact]
    public void Constructor_ShouldCreate_WhenValidStartedAt()
    {
        var summary = new ExecutionSummary(ValidStart);

        summary.StartedAt.Should().Be(ValidStart);
        summary.FinishedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenStartedAtIsDefault()
    {
        var act = () => new ExecutionSummary(default);

        act.Should().Throw<ArgumentException>().WithParameterName("startedAt");
    }

    [Fact]
    public void Counters_ShouldDefaultToZero()
    {
        var summary = new ExecutionSummary(ValidStart);

        summary.TotalProcessed.Should().Be(0);
        summary.TotalClosed.Should().Be(0);
        summary.TotalSimulated.Should().Be(0);
        summary.TotalSkipped.Should().Be(0);
        summary.TotalErrors.Should().Be(0);
    }

    [Fact]
    public void FinishedAt_ShouldBeSettable()
    {
        var summary = new ExecutionSummary(ValidStart);
        var finishedAt = ValidStart.AddMinutes(5);

        summary.FinishedAt = finishedAt;

        summary.FinishedAt.Should().Be(finishedAt);
    }

    [Fact]
    public void Counters_ShouldBeIncrementable()
    {
        var summary = new ExecutionSummary(ValidStart);

        summary.TotalProcessed++;
        summary.TotalClosed++;
        summary.TotalSimulated += 2;
        summary.TotalSkipped = 5;
        summary.TotalErrors = 1;

        summary.TotalProcessed.Should().Be(1);
        summary.TotalClosed.Should().Be(1);
        summary.TotalSimulated.Should().Be(2);
        summary.TotalSkipped.Should().Be(5);
        summary.TotalErrors.Should().Be(1);
    }
}
