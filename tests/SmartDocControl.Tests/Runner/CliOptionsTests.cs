using FluentAssertions;
using SmartDocControl.Runner;
using Xunit;

namespace SmartDocControl.Tests.Runner;

public sealed class CliOptionsTests
{
    [Fact]
    public void Parse_EnvironmentOnly_ReturnsSuccess()
    {
        var result = CliParseResult.Parse(["--environment", "TST"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.Environment.Should().Be("TST");
        result.Options.ValidateOnly.Should().BeFalse();
        result.Options.ShowHelp.Should().BeFalse();
    }

    [Fact]
    public void Parse_MissingEnvironment_ReturnsError()
    {
        var result = CliParseResult.Parse([]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("--environment");
    }

    [Fact]
    public void Parse_WithValidateOnly_SetsFlag()
    {
        var result = CliParseResult.Parse(["--environment", "TST", "--validate-only"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.ValidateOnly.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithDryRun_SetsValidateOnlyAlias()
    {
        var result = CliParseResult.Parse(["--environment", "DEV", "--dry-run"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.ValidateOnly.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithHelp_ShowsHelpWithoutEnvironmentRequired()
    {
        var result = CliParseResult.Parse(["--help"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.ShowHelp.Should().BeTrue();
    }

    [Fact]
    public void Parse_UnknownArgument_ReturnsError()
    {
        var result = CliParseResult.Parse(["--environment", "TST", "--unknown-flag"]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("--unknown-flag");
    }

    [Fact]
    public void Parse_EnvironmentFlagWithoutValue_ReturnsError()
    {
        var result = CliParseResult.Parse(["--environment"]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("--environment");
    }

    [Fact]
    public void Parse_ShortFormEnvironment_ReturnsSuccess()
    {
        var result = CliParseResult.Parse(["-e", "PRD"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.Environment.Should().Be("PRD");
    }
}
