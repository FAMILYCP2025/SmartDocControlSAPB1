using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SchemaInstallerPlanTests
{
    private readonly SchemaInstaller _installer = new();

    // ─── UDT planning ────────────────────────────────────────────────────────

    [Fact]
    public async Task Plan_UdtDoesNotExist_ActionIsCreate()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "bott_NoObject")]);
        var metadata = new InMemorySapMetadata();

        var plan = await _installer.PlanAsync(schema, metadata);

        var entry = plan.Entries.Should().ContainSingle().Subject;
        entry.ObjectType.Should().Be(InstallObjectType.UserTable);
        entry.ObjectName.Should().Be("JCA_DLC_RULE");
        entry.Action.Should().Be(InstallAction.Create);
        entry.IsBlocking.Should().BeFalse();
    }

    [Fact]
    public async Task Plan_UdtExistsCompatible_ActionIsSkip()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "bott_NoObject")]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });

        var plan = await _installer.PlanAsync(schema, metadata);

        var entry = plan.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(InstallAction.Skip);
        entry.IsBlocking.Should().BeFalse();
    }

    // ─── UDF planning ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Plan_UdfDoesNotExist_ActionIsCreate()
    {
        var schema = BuildSchema(udfs: [Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1)]);
        var metadata = new InMemorySapMetadata();

        var plan = await _installer.PlanAsync(schema, metadata);

        var entry = plan.Entries.Should().ContainSingle().Subject;
        entry.ObjectType.Should().Be(InstallObjectType.UserField);
        entry.ObjectName.Should().Be("JCA_DLC_RULE.U_Active");
        entry.Action.Should().Be(InstallAction.Create);
        entry.IsBlocking.Should().BeFalse();
    }

    [Fact]
    public async Task Plan_UdfExistsCompatibleExactSize_ActionIsSkip()
    {
        var schema = BuildSchema(udfs: [Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1)]);
        var metadata = new InMemorySapMetadata();
        metadata.AddField(new SapFieldMetadata
            { TableName = "JCA_DLC_RULE", FieldName = "Active", Type = "db_Alpha", Size = 1 });

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.Entries.Should().ContainSingle(e => e.Action == InstallAction.Skip);
    }

    [Fact]
    public async Task Plan_UdfExistsLargerSize_ActionIsSkip()
    {
        var schema = BuildSchema(udfs: [Udf("JCA_DLC_RULE", "CardCode", "db_Alpha", 15)]);
        var metadata = new InMemorySapMetadata();
        metadata.AddField(new SapFieldMetadata
            { TableName = "JCA_DLC_RULE", FieldName = "CardCode", Type = "db_Alpha", Size = 50 });

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.Entries.Should().ContainSingle(e => e.Action == InstallAction.Skip && !e.IsBlocking);
    }

    // ─── Mixed plan ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Plan_MixedCreateAndSkip_BothPresent()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_RULE", "bott_NoObject"),
                Udt("JCA_DLC_EXC", "bott_NoObject")
            ]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.Entries.Should().HaveCount(2);
        plan.Entries.Should().ContainSingle(e => e.Action == InstallAction.Create && e.ObjectName == "JCA_DLC_EXC");
        plan.Entries.Should().ContainSingle(e => e.Action == InstallAction.Skip  && e.ObjectName == "JCA_DLC_RULE");
    }

    [Fact]
    public async Task Plan_OrderMatchesDescriptorOrder()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_SCHEMA", "bott_NoObject"),
                Udt("JCA_DLC_RULE", "bott_NoObject"),
                Udt("JCA_DLC_EXC", "bott_NoObject")
            ]);
        var metadata = new InMemorySapMetadata();

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.Entries.Select(e => e.ObjectName)
            .Should().Equal("JCA_DLC_SCHEMA", "JCA_DLC_RULE", "JCA_DLC_EXC");
    }

    // ─── Counters ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Plan_Counters_ReflectPlanContents()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_NEW", "bott_NoObject"),
                Udt("JCA_DLC_EXISTING", "bott_NoObject")
            ],
            udfs:
            [
                Udf("JCA_DLC_NEW", "Active", "db_Alpha", 1)
            ]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_EXISTING", TableType = "bott_NoObject" });

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.TotalCreates.Should().Be(2);
        plan.TotalSkips.Should().Be(1);
        plan.TotalDrifts.Should().Be(0);
        plan.HasBlockingIssues.Should().BeFalse();
    }

    [Fact]
    public async Task Plan_EmptySchema_EmptyPlan()
    {
        var schema = BuildSchema();
        var metadata = new InMemorySapMetadata();

        var plan = await _installer.PlanAsync(schema, metadata);

        plan.Entries.Should().BeEmpty();
        plan.HasBlockingIssues.Should().BeFalse();
        plan.TotalCreates.Should().Be(0);
        plan.TotalSkips.Should().Be(0);
        plan.TotalDrifts.Should().Be(0);
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
