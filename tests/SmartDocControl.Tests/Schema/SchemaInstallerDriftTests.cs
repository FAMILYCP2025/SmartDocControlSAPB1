using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SchemaInstallerDriftTests
{
    private readonly SchemaInstaller _installer = new();

    // ─── UDT drift ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Plan_UdtTableTypeDiffers_ActionIsDriftBlocking()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "noObject")]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "Document" });

        var plan = await _installer.PlanAsync(schema, metadata);

        var entry = plan.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(InstallAction.Drift);
        entry.IsBlocking.Should().BeTrue();
        entry.Reason.Should().Contain("TableType");
        entry.Reason.Should().Contain("Document");
        entry.Reason.Should().Contain("noObject");
    }

    // ─── UDF drift ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Plan_UdfTypeDiffers_ActionIsDriftBlocking()
    {
        var schema = BuildSchema(udfs: [Udf("JCA_DLC_RULE", "GraceDays", "db_Numeric", 6)]);
        var metadata = new InMemorySapMetadata();
        metadata.AddField(new SapFieldMetadata
            { TableName = "JCA_DLC_RULE", FieldName = "GraceDays", Type = "db_Alpha", Size = 6 });

        var plan = await _installer.PlanAsync(schema, metadata);

        var entry = plan.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(InstallAction.Drift);
        entry.IsBlocking.Should().BeTrue();
        entry.Reason.Should().Contain("db_Numeric");
        entry.Reason.Should().Contain("db_Alpha");
    }

    [Fact]
    public async Task Plan_UdfSizeSmallerThanRequired_ActionIsDriftBlocking()
    {
        var schema = BuildSchema(udfs: [Udf("JCA_DLC_RULE", "CardCode", "db_Alpha", 15)]);
        var metadata = new InMemorySapMetadata();
        metadata.AddField(new SapFieldMetadata
            { TableName = "JCA_DLC_RULE", FieldName = "CardCode", Type = "db_Alpha", Size = 10 });

        var plan = await _installer.PlanAsync(schema, metadata);

        var entry = plan.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(InstallAction.Drift);
        entry.IsBlocking.Should().BeTrue();
        entry.Reason.Should().Contain("15");
        entry.Reason.Should().Contain("10");
    }

    // ─── Multiple drifts ──────────────────────────────────────────────────────

    [Fact]
    public async Task Plan_MultipleDrifts_AllDetected()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE", "noObject")],
            udfs:
            [
                Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1),
                Udf("JCA_DLC_RULE", "GraceDays", "db_Numeric", 6)
            ]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "MasterData" });
        metadata.AddField(new SapFieldMetadata
            { TableName = "JCA_DLC_RULE", FieldName = "Active", Type = "db_Numeric", Size = 1 });
        metadata.AddField(new SapFieldMetadata
            { TableName = "JCA_DLC_RULE", FieldName = "GraceDays", Type = "db_Numeric", Size = 3 });

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.TotalDrifts.Should().Be(3);
        plan.Entries.Should().OnlyContain(e => e.Action == InstallAction.Drift);
    }

    [Fact]
    public async Task Plan_AnyDrift_HasBlockingIssuesIsTrue()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_NEW", "noObject"), Udt("JCA_DLC_DRIFT", "noObject")]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_DRIFT", TableType = "Document" });

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.HasBlockingIssues.Should().BeTrue();
        plan.TotalCreates.Should().Be(1);
        plan.TotalDrifts.Should().Be(1);
    }

    [Fact]
    public async Task Plan_NoDrift_HasBlockingIssuesIsFalse()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "noObject")]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "noObject" });

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.HasBlockingIssues.Should().BeFalse();
    }

    [Fact]
    public async Task Plan_DriftEntry_ObjectNameIncludesTableName()
    {
        var schema = BuildSchema(udfs: [Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1)]);
        var metadata = new InMemorySapMetadata();
        metadata.AddField(new SapFieldMetadata
            { TableName = "JCA_DLC_RULE", FieldName = "Active", Type = "db_Numeric", Size = 1 });

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.Entries.Should().ContainSingle(e =>
            e.ObjectName == "JCA_DLC_RULE.U_Active" &&
            e.Action == InstallAction.Drift);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static LoadedSchema BuildSchema(
        UdtDescriptor[]? udts = null,
        UdfDescriptor[]? udfs = null) =>
        new(
            new SchemaManifest { SchemaVersion = "1.0.0", Steps = Array.Empty<string>() },
            (IReadOnlyList<UdtDescriptor>)(udts ?? []),
            (IReadOnlyList<UdfDescriptor>)(udfs ?? []));

    private static UdtDescriptor Udt(string name, string tableType) => new()
    {
        Type = "UserTable", Operation = "CreateIfNotExists",
        TableName = name, TableDescription = name, TableType = tableType
    };

    private static UdfDescriptor Udf(string table, string name, string type, int size) => new()
    {
        TableName = table, Name = name,
        FieldDescription = name, Type = type, Size = size
    };
}
