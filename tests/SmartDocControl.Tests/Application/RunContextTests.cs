using FluentAssertions;
using SmartDocControl.Application.Models;
using Xunit;

namespace SmartDocControl.Tests.Application;

public class RunContextTests
{
    private static readonly DateTime ValidDate = new(2026, 5, 6, 8, 0, 0);

    [Fact]
    public void Constructor_ShouldCreate_WhenValidParameters()
    {
        var ctx = new RunContext("RUN-001", ValidDate, simulationMode: true, maxDocumentsPerRun: 50, "TST");

        ctx.CorrelationId.Should().Be("RUN-001");
        ctx.ExecutionDate.Should().Be(ValidDate);
        ctx.SimulationMode.Should().BeTrue();
        ctx.MaxDocumentsPerRun.Should().Be(50);
        ctx.EnvironmentName.Should().Be("TST");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenCorrelationIdIsEmpty(string correlationId)
    {
        var act = () => new RunContext(correlationId, ValidDate, false, 100, "TST");

        act.Should().Throw<ArgumentException>().WithParameterName("correlationId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenEnvironmentNameIsEmpty(string environmentName)
    {
        var act = () => new RunContext("RUN-001", ValidDate, false, 100, environmentName);

        act.Should().Throw<ArgumentException>().WithParameterName("environmentName");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenExecutionDateIsDefault()
    {
        var act = () => new RunContext("RUN-001", default, false, 100, "TST");

        act.Should().Throw<ArgumentException>().WithParameterName("executionDate");
    }
}
