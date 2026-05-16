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

    [Fact]
    public void Load_SchemaTrackingUdfFile_DeserializesEightFields()
    {
        // Mirrors scripts/install/schema/v1/004_udf_schema_fields.json.
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            { "schemaVersion": "1.0.0", "steps": ["004_udf_schema_fields.json"] }
            """);
        WriteFile(dir, "004_udf_schema_fields.json", """
            {
              "type": "UserFields",
              "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_SCHEMA",
              "fields": [
                { "name": "SchemaVersion",   "fieldDescription": "Schema version", "type": "db_Alpha",   "size": 20, "mandatory": true },
                { "name": "AppVersion",      "fieldDescription": "App version",    "type": "db_Alpha",   "size": 20 },
                { "name": "Environment",     "fieldDescription": "Environment",    "type": "db_Alpha",   "size": 10, "mandatory": true },
                { "name": "AppliedAtUtc",    "fieldDescription": "Applied UTC",    "type": "db_Alpha",   "size": 30, "mandatory": true },
                { "name": "RequiredObjects", "fieldDescription": "Required objs",  "type": "db_Numeric", "size": 6,  "mandatory": true },
                { "name": "VerifiedObjects", "fieldDescription": "Verified objs",  "type": "db_Numeric", "size": 6,  "mandatory": true },
                { "name": "Status",          "fieldDescription": "Install status", "type": "db_Alpha",   "size": 20, "mandatory": true },
                { "name": "RunId",           "fieldDescription": "Run ID",         "type": "db_Alpha",   "size": 20 }
              ]
            }
            """);

        var result = _loader.Load(dir);

        result.UserFields.Should().HaveCount(8);
        result.UserFields.Select(f => f.Name).Should().BeEquivalentTo(new[]
        {
            "SchemaVersion", "AppVersion", "Environment", "AppliedAtUtc",
            "RequiredObjects", "VerifiedObjects", "Status", "RunId"
        });
        result.UserFields.Should().AllSatisfy(f => f.TableName.Should().Be("JCA_DLC_SCHEMA"));
    }

    [Fact]
    public void Load_FullSchemaV1Layout_Returns2UdtsAnd14Udfs()
    {
        // Simulates the production schema/v1 layout: 2 UDTs + 6 UDFs (RULE) + 8 UDFs (SCHEMA) = 16 objects.
        var dir = CreateTempDir();
        WriteFile(dir, "manifest.json", """
            {
              "schemaVersion": "1.0.0",
              "steps": [
                "001_udt_schema_version.json",
                "002_udt_rule.json",
                "003_udf_rule_fields.json",
                "004_udf_schema_fields.json"
              ]
            }
            """);
        WriteFile(dir, "001_udt_schema_version.json", """
            { "type": "UserTable", "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_SCHEMA", "tableDescription": "SDC schema registry", "tableType": "bott_NoObject" }
            """);
        WriteFile(dir, "002_udt_rule.json", """
            { "type": "UserTable", "operation": "CreateIfNotExists",
              "tableName": "JCA_DLC_RULE", "tableDescription": "Document close rules", "tableType": "bott_NoObject" }
            """);
        WriteFile(dir, "003_udf_rule_fields.json", """
            {
              "type": "UserFields", "operation": "CreateIfNotExists", "tableName": "JCA_DLC_RULE",
              "fields": [
                { "name": "Active",             "fieldDescription": "Active",   "type": "db_Alpha",   "size": 1 },
                { "name": "ObjType",            "fieldDescription": "Obj type", "type": "db_Alpha",   "size": 20, "mandatory": true },
                { "name": "EntitySet",          "fieldDescription": "Set",      "type": "db_Alpha",   "size": 50, "mandatory": true },
                { "name": "GraceDays",          "fieldDescription": "Grace",    "type": "db_Numeric", "size": 6,  "mandatory": true },
                { "name": "Simulation",         "fieldDescription": "Sim",      "type": "db_Alpha",   "size": 1 },
                { "name": "MaxDocumentsPerRun", "fieldDescription": "MaxDocs",  "type": "db_Numeric", "size": 6 }
              ]
            }
            """);
        WriteFile(dir, "004_udf_schema_fields.json", """
            {
              "type": "UserFields", "operation": "CreateIfNotExists", "tableName": "JCA_DLC_SCHEMA",
              "fields": [
                { "name": "SchemaVersion",   "fieldDescription": "Schema version", "type": "db_Alpha",   "size": 20, "mandatory": true },
                { "name": "AppVersion",      "fieldDescription": "App version",    "type": "db_Alpha",   "size": 20 },
                { "name": "Environment",     "fieldDescription": "Environment",    "type": "db_Alpha",   "size": 10, "mandatory": true },
                { "name": "AppliedAtUtc",    "fieldDescription": "Applied UTC",    "type": "db_Alpha",   "size": 30, "mandatory": true },
                { "name": "RequiredObjects", "fieldDescription": "Required objs",  "type": "db_Numeric", "size": 6,  "mandatory": true },
                { "name": "VerifiedObjects", "fieldDescription": "Verified objs",  "type": "db_Numeric", "size": 6,  "mandatory": true },
                { "name": "Status",          "fieldDescription": "Install status", "type": "db_Alpha",   "size": 20, "mandatory": true },
                { "name": "RunId",           "fieldDescription": "Run ID",         "type": "db_Alpha",   "size": 20 }
              ]
            }
            """);

        var result = _loader.Load(dir);

        result.UserTables.Should().HaveCount(2);
        result.UserFields.Should().HaveCount(14);
        (result.UserTables.Count + result.UserFields.Count).Should().Be(16);
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
