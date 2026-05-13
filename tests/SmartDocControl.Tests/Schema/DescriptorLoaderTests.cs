using System.Text.Json;
using FluentAssertions;
using SmartDocControl.Schema.Loader;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class DescriptorLoaderTests : IDisposable
{
    private readonly List<string> _tempDirs = new();
    private readonly DescriptorLoader _loader = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private string CreateTempDir()
    {
        var dir = Directory.CreateTempSubdirectory("smartdoc_schema_test_").FullName;
        _tempDirs.Add(dir);
        return dir;
    }

    private static void WriteFile(string dir, string name, string content) =>
        File.WriteAllText(Path.Combine(dir, name), content);

    // ─── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public void Load_ValidManifest_ReturnsSchemaVersion()
    {
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            {
              "schemaVersion": "1.0.0",
              "appVersionMinimum": "0.2.0",
              "description": "Test schema",
              "steps": ["001_udt_rule.json"]
            }
            """);
        WriteFile(dir, "001_udt_rule.json", """
            {
              "type": "UserTable",
              "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_RULE",
              "tableDescription": "Rules table",
              "tableType": "bott_NoObject"
            }
            """);

        var result = _loader.Load(dir);

        result.Manifest.SchemaVersion.Should().Be("1.0.0");
        result.Manifest.AppVersionMinimum.Should().Be("0.2.0");
        result.Manifest.Description.Should().Be("Test schema");
    }

    [Fact]
    public void Load_MultipleSteps_LoadsInManifestOrder()
    {
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            {
              "schemaVersion": "1.0.0",
              "steps": ["002_udt_b.json", "001_udt_a.json"]
            }
            """);
        WriteFile(dir, "001_udt_a.json", """
            {
              "type": "UserTable", "operation": "CreateIfNotExists",
              "tableName": "JCA_TABLE_A", "tableDescription": "Table A", "tableType": "bott_NoObject"
            }
            """);
        WriteFile(dir, "002_udt_b.json", """
            {
              "type": "UserTable", "operation": "CreateIfNotExists",
              "tableName": "JCA_TABLE_B", "tableDescription": "Table B", "tableType": "bott_NoObject"
            }
            """);

        var result = _loader.Load(dir);

        result.UserTables.Should().HaveCount(2);
        result.UserTables[0].TableName.Should().Be("JCA_TABLE_B");
        result.UserTables[1].TableName.Should().Be("JCA_TABLE_A");
    }

    [Fact]
    public void Load_UdtDescriptor_AllFieldsDeserialized()
    {
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            { "schemaVersion": "1.0.0", "steps": ["001_udt.json"] }
            """);
        WriteFile(dir, "001_udt.json", """
            {
              "type": "UserTable",
              "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_RULE",
              "tableDescription": "Document close rules",
              "tableType": "bott_NoObject"
            }
            """);

        var result = _loader.Load(dir);

        result.UserTables.Should().HaveCount(1);
        var udt = result.UserTables[0];
        udt.Type.Should().Be("UserTable");
        udt.Operation.Should().Be("CreateIfNotExists");
        udt.TableName.Should().Be("JCA_DLC_RULE");
        udt.TableDescription.Should().Be("Document close rules");
        udt.TableType.Should().Be("bott_NoObject");
    }

    [Fact]
    public void Load_UdfFile_TableNamePropagatedToEachField()
    {
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            { "schemaVersion": "1.0.0", "steps": ["001_udf.json"] }
            """);
        WriteFile(dir, "001_udf.json", """
            {
              "type": "UserFields",
              "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_RULE",
              "fields": [
                { "name": "Active", "fieldDescription": "Rule active", "type": "db_Alpha", "size": 1 },
                { "name": "GraceDays", "fieldDescription": "Grace days", "type": "db_Numeric", "size": 6 }
              ]
            }
            """);

        var result = _loader.Load(dir);

        result.UserFields.Should().HaveCount(2);
        result.UserFields[0].TableName.Should().Be("JCA_DLC_RULE");
        result.UserFields[0].Name.Should().Be("Active");
        result.UserFields[1].TableName.Should().Be("JCA_DLC_RULE");
        result.UserFields[1].Name.Should().Be("GraceDays");
    }

    [Fact]
    public void Load_UdfWithValidValues_DeserializedCorrectly()
    {
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            { "schemaVersion": "1.0.0", "steps": ["001_udf.json"] }
            """);
        WriteFile(dir, "001_udf.json", """
            {
              "type": "UserFields",
              "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_RULE",
              "fields": [
                {
                  "name": "Active",
                  "fieldDescription": "Rule active",
                  "type": "db_Alpha",
                  "size": 1,
                  "defaultValue": "Y",
                  "validValues": [
                    { "value": "Y", "description": "Yes" },
                    { "value": "N", "description": "No" }
                  ]
                }
              ]
            }
            """);

        var result = _loader.Load(dir);

        var field = result.UserFields.Should().ContainSingle().Subject;
        field.DefaultValue.Should().Be("Y");
        field.ValidValues.Should().HaveCount(2);
        field.ValidValues![0].Value.Should().Be("Y");
        field.ValidValues[1].Value.Should().Be("N");
    }

    [Fact]
    public void Load_MixedStepTypes_SeparatesUdtsAndUdfs()
    {
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            { "schemaVersion": "1.0.0", "steps": ["001_udt.json", "002_udf.json"] }
            """);
        WriteFile(dir, "001_udt.json", """
            {
              "type": "UserTable", "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_RULE", "tableDescription": "Rules", "tableType": "bott_NoObject"
            }
            """);
        WriteFile(dir, "002_udf.json", """
            {
              "type": "UserFields", "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_RULE",
              "fields": [
                { "name": "Active", "fieldDescription": "Active", "type": "db_Alpha", "size": 1 }
              ]
            }
            """);

        var result = _loader.Load(dir);

        result.UserTables.Should().HaveCount(1);
        result.UserFields.Should().HaveCount(1);
    }

    // ─── Error paths ──────────────────────────────────────────────────────────

    [Fact]
    public void Load_FolderDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        var act = () => _loader.Load(@"C:\does_not_exist_7f3a9b2c");

        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*does_not_exist_7f3a9b2c*");
    }

    [Fact]
    public void Load_ManifestNotFound_ThrowsFileNotFoundException()
    {
        var dir = CreateTempDir();

        var act = () => _loader.Load(dir);

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*manifest.json*");
    }

    [Fact]
    public void Load_ManifestInvalidJson_ThrowsJsonException()
    {
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", "{ this is not valid json }");

        var act = () => _loader.Load(dir);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Load_MissingStepFile_ThrowsFileNotFoundException()
    {
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            { "schemaVersion": "1.0.0", "steps": ["missing_step.json"] }
            """);

        var act = () => _loader.Load(dir);

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*missing_step.json*");
    }
}
