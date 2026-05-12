using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Loader;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class DescriptorValidatorTests
{
    private readonly DescriptorValidator _validator = new();

    // ─── UdtDescriptor ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidUdt_DoesNotThrow()
    {
        var udt = ValidUdt();
        var act = () => _validator.Validate(udt);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_UdtEmptyTableName_Throws()
    {
        var udt = ValidUdt() with { TableName = "" };
        var act = () => _validator.Validate(udt);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*TableName is required*");
    }

    [Fact]
    public void Validate_UdtTableNameExceedsMaxLength_Throws()
    {
        var udt = ValidUdt() with { TableName = "JCA_DLC_TABLE_NAME_TOO_LONG_123" };
        var act = () => _validator.Validate(udt);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*exceeds*maximum length*20*");
    }

    [Fact]
    public void Validate_UdtTableDescriptionAtMaxLength_DoesNotThrow()
    {
        var udt = ValidUdt() with { TableDescription = new string('A', 30) };
        var act = () => _validator.Validate(udt);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_UdtTableDescriptionExceedsMaxLength_Throws()
    {
        var udt = ValidUdt() with { TableDescription = new string('A', 31) };
        var act = () => _validator.Validate(udt);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*exceeds*maximum length*30*");
    }

    [Fact]
    public void Validate_UdtEmptyTableDescription_Throws()
    {
        var udt = ValidUdt() with { TableDescription = "" };
        var act = () => _validator.Validate(udt);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*TableDescription is required*");
    }

    [Fact]
    public void Validate_UdtEmptyOperation_Throws()
    {
        var udt = ValidUdt() with { Operation = "" };
        var act = () => _validator.Validate(udt);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*Operation is required*");
    }

    [Fact]
    public void Validate_UdtUnsupportedOperation_Throws()
    {
        var udt = ValidUdt() with { Operation = "DeleteIfExists" };
        var act = () => _validator.Validate(udt);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*Operation 'DeleteIfExists' is not supported*");
    }

    [Fact]
    public void Validate_UdtUnsupportedTableType_Throws()
    {
        var udt = ValidUdt() with { TableType = "Document" };
        var act = () => _validator.Validate(udt);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*TableType 'Document' is not recognized*");
    }

    // ─── UdfDescriptor ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidUdf_DoesNotThrow()
    {
        var udf = ValidUdf();
        var act = () => _validator.Validate(udf);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_UdfEmptyTableName_Throws()
    {
        var udf = ValidUdf() with { TableName = "" };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*TableName is required*");
    }

    [Fact]
    public void Validate_UdfEmptyName_Throws()
    {
        var udf = ValidUdf() with { Name = "" };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*Name is required*");
    }

    [Fact]
    public void Validate_UdfNameWithUPrefix_Throws()
    {
        var udf = ValidUdf() with { Name = "U_Active" };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*must not include the 'U_' prefix*");
    }

    [Fact]
    public void Validate_UdfNameWithLowercaseUPrefix_Throws()
    {
        var udf = ValidUdf() with { Name = "u_active" };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*must not include the 'U_' prefix*");
    }

    [Fact]
    public void Validate_UdfEmptyFieldDescription_Throws()
    {
        var udf = ValidUdf() with { FieldDescription = "" };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*FieldDescription is required*");
    }

    [Fact]
    public void Validate_UdfUnknownSapType_Throws()
    {
        var udf = ValidUdf() with { Type = "db_Unknown" };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*'db_Unknown' is not recognized*");
    }

    [Fact]
    public void Validate_DbAlphaWithoutSize_Throws()
    {
        var udf = ValidUdf() with { Type = "db_Alpha", Size = null };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*Size is required*db_Alpha*");
    }

    [Fact]
    public void Validate_DbAlphaWithZeroSize_Throws()
    {
        var udf = ValidUdf() with { Type = "db_Alpha", Size = 0 };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*Size is required*db_Alpha*");
    }

    [Fact]
    public void Validate_DbNumericWithoutSize_Throws()
    {
        var udf = ValidUdf() with { Type = "db_Numeric", Size = null };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*Size is required*db_Numeric*");
    }

    [Fact]
    public void Validate_DbNumericWithZeroSize_Throws()
    {
        var udf = ValidUdf() with { Type = "db_Numeric", Size = 0 };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*Size is required*db_Numeric*");
    }

    [Fact]
    public void Validate_DbFloatWithoutSize_DoesNotThrow()
    {
        var udf = ValidUdf() with { Type = "db_Float", Size = null };
        var act = () => _validator.Validate(udf);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DbDateWithoutSize_DoesNotThrow()
    {
        var udf = ValidUdf() with { Type = "db_Date", Size = null };
        var act = () => _validator.Validate(udf);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DbMemoWithoutSize_DoesNotThrow()
    {
        var udf = ValidUdf() with { Type = "db_Memo", Size = null };
        var act = () => _validator.Validate(udf);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_UdfDuplicateValidValues_Throws()
    {
        var udf = ValidUdf() with
        {
            ValidValues = new List<ValidValueDescriptor>
            {
                new() { Value = "Y", Description = "Yes" },
                new() { Value = "Y", Description = "Also Yes" }
            }
        };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*duplicate value 'Y'*");
    }

    [Fact]
    public void Validate_UdfDuplicateValidValues_CaseInsensitive_Throws()
    {
        var udf = ValidUdf() with
        {
            ValidValues = new List<ValidValueDescriptor>
            {
                new() { Value = "y", Description = "lowercase" },
                new() { Value = "Y", Description = "uppercase" }
            }
        };
        var act = () => _validator.Validate(udf);
        act.Should().Throw<DescriptorValidationException>();
    }

    // ─── SchemaManifest ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidManifest_DoesNotThrow()
    {
        var manifest = ValidManifest();
        var act = () => _validator.Validate(manifest);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ManifestEmptySchemaVersion_Throws()
    {
        var manifest = ValidManifest() with { SchemaVersion = "" };
        var act = () => _validator.Validate(manifest);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*SchemaVersion is required*");
    }

    [Fact]
    public void Validate_ManifestEmptySteps_Throws()
    {
        var manifest = ValidManifest() with { Steps = Array.Empty<string>() };
        var act = () => _validator.Validate(manifest);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*Steps cannot be empty*");
    }

    [Fact]
    public void Validate_ManifestStepWithoutJsonExtension_Throws()
    {
        var manifest = ValidManifest() with { Steps = new[] { "001_udt_rule" } };
        var act = () => _validator.Validate(manifest);
        act.Should().Throw<DescriptorValidationException>()
            .WithMessage("*must end with .json*");
    }

    // ─── Factories ────────────────────────────────────────────────────────────

    private static UdtDescriptor ValidUdt() => new()
    {
        Type = "UserTable",
        Operation = "CreateIfNotExists",
        TableName = "JCA_DLC_RULE",
        TableDescription = "Document close rules",
        TableType = "noObject"
    };

    private static UdfDescriptor ValidUdf() => new()
    {
        TableName = "JCA_DLC_RULE",
        Name = "Active",
        FieldDescription = "Rule is active",
        Type = "db_Alpha",
        Size = 1
    };

    private static SchemaManifest ValidManifest() => new()
    {
        SchemaVersion = "1.0.0",
        AppVersionMinimum = "0.2.0",
        Description = "Test",
        Steps = new[] { "001_udt_rule.json" }
    };
}
