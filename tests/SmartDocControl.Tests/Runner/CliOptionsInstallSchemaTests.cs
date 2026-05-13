using FluentAssertions;
using SmartDocControl.Runner;
using Xunit;

namespace SmartDocControl.Tests.Runner;

public sealed class CliOptionsInstallSchemaTests
{
    [Fact]
    public void Parse_InstallSchemaWithDryRun_SetsBothFlags()
    {
        var result = CliParseResult.Parse(["--environment", "TST", "--install-schema", "--dry-run"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.InstallSchema.Should().BeTrue();
        result.Options.DryRun.Should().BeTrue();
        result.Options.ValidateOnly.Should().BeFalse();
    }

    [Fact]
    public void Parse_InstallSchemaWithoutDryRun_StillParses_DryRunIsFalse()
    {
        // Parser is pure: it accepts --install-schema alone. The command layer
        // enforces that real apply requires --force; this is just the parser
        // contract.
        var result = CliParseResult.Parse(["--environment", "TST", "--install-schema"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.InstallSchema.Should().BeTrue();
        result.Options.DryRun.Should().BeFalse();
    }

    [Fact]
    public void Parse_InstallSchemaWithForce_SetsForceFlag()
    {
        var result = CliParseResult.Parse(["--environment", "TST", "--install-schema", "--dry-run", "--force"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.Force.Should().BeTrue();
    }

    [Fact]
    public void Parse_DryRunStandalone_StillImpliesValidateOnly_BackwardCompat()
    {
        var result = CliParseResult.Parse(["--environment", "TST", "--dry-run"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.ValidateOnly.Should().BeTrue();
        result.Options.InstallSchema.Should().BeFalse();
        result.Options.DryRun.Should().BeTrue();
    }

    [Fact]
    public void Parse_ValidateOnlyAndInstallSchema_ReturnsError()
    {
        var result = CliParseResult.Parse(["--environment", "TST", "--validate-only", "--install-schema"]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Conflicting modes");
    }

    [Fact]
    public void Parse_ForceWithoutInstallSchema_StillParses()
    {
        // Parser is pure; semantic conflicts (force without install) are not enforced here.
        var result = CliParseResult.Parse(["--environment", "TST", "--force"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.Force.Should().BeTrue();
        result.Options.InstallSchema.Should().BeFalse();
    }

    [Fact]
    public void Parse_InstallSchemaWithoutEnvironment_ReturnsError()
    {
        var result = CliParseResult.Parse(["--install-schema", "--dry-run"]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("--environment");
    }

    [Fact]
    public void Parse_HelpWithInstallSchema_ReturnsShowHelp()
    {
        var result = CliParseResult.Parse(["--help", "--install-schema"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.ShowHelp.Should().BeTrue();
        result.Options.InstallSchema.Should().BeTrue();
    }

    [Fact]
    public void Parse_UnknownFlag_ReturnsError()
    {
        var result = CliParseResult.Parse(["--environment", "TST", "--install-schema", "--apply-now"]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("--apply-now");
    }

    [Fact]
    public void Parse_TraceMetadata_SetsFlag()
    {
        var result = CliParseResult.Parse(
            ["--environment", "TST", "--install-schema", "--force", "--trace-metadata"]);

        result.IsSuccess.Should().BeTrue();
        result.Options!.TraceMetadata.Should().BeTrue();
        result.Options.InstallSchema.Should().BeTrue();
    }
}
