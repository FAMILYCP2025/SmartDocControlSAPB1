using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SchemaInstallerApplyTests
{
    private readonly SchemaInstaller _installer = new();

    [Fact]
    public async Task ApplyAsync_OnlyCreates_AllExecuted()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_RULE", "noObject"),
                Udt("JCA_DLC_EXC", "noObject")
            ]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor();

        var result = await _installer.ApplyAsync(plan, schema, executor);

        result.IsSuccessful.Should().BeTrue();
        result.WasAborted.Should().BeFalse();
        result.TotalCreated.Should().Be(2);
        executor.CreatedTables.Should().Equal("JCA_DLC_RULE", "JCA_DLC_EXC");
    }

    [Fact]
    public async Task ApplyAsync_MixedCreateAndSkip_OnlyCreatesExecuted()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_RULE", "noObject"),
                Udt("JCA_DLC_EXISTING", "noObject")
            ]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_EXISTING", TableType = "noObject" });

        var plan = await _installer.PlanAsync(schema, metadata);
        var executor = new InMemorySchemaExecutor();

        var result = await _installer.ApplyAsync(plan, schema, executor);

        result.IsSuccessful.Should().BeTrue();
        result.TotalCreated.Should().Be(1);
        result.TotalSkipped.Should().Be(1);
        executor.CreatedTables.Should().ContainSingle().Which.Should().Be("JCA_DLC_RULE");
    }

    [Fact]
    public async Task ApplyAsync_CreateUdf_ResolvesDescriptorAndCallsExecutor()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE", "noObject")],
            udfs: [Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1)]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor();

        await _installer.ApplyAsync(plan, schema, executor);

        executor.CreatedTables.Should().ContainSingle().Which.Should().Be("JCA_DLC_RULE");
        executor.CreatedFields.Should().ContainSingle().Which.Should().Be("JCA_DLC_RULE.U_Active");
    }

    [Fact]
    public async Task ApplyAsync_EmptyPlan_ReturnsEmptyResult()
    {
        var schema = BuildSchema();
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor();

        var result = await _installer.ApplyAsync(plan, schema, executor);

        result.Entries.Should().BeEmpty();
        result.IsSuccessful.Should().BeTrue();
        result.WasAborted.Should().BeFalse();
        executor.CreatedTables.Should().BeEmpty();
        executor.CreatedFields.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_OrderMatchesPlan()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_SCHEMA", "noObject"),
                Udt("JCA_DLC_RULE", "noObject"),
                Udt("JCA_DLC_EXC", "noObject")
            ]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor();

        var result = await _installer.ApplyAsync(plan, schema, executor);

        result.Entries.Select(e => e.ObjectName)
            .Should().Equal("JCA_DLC_SCHEMA", "JCA_DLC_RULE", "JCA_DLC_EXC");
        executor.CreatedTables.Should().Equal("JCA_DLC_SCHEMA", "JCA_DLC_RULE", "JCA_DLC_EXC");
    }

    [Fact]
    public async Task ApplyAsync_AlreadyExistsWithFlagTrue_CountsAsAlreadyExists()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "noObject")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor
        {
            TableHook = _ => new SapObjectAlreadyExistsException("JCA_DLC_RULE", "-2035", "exists")
        };

        var result = await _installer.ApplyAsync(plan, schema, executor,
            new ApplyOptions { TreatAlreadyExistsAsSuccess = true });

        result.IsSuccessful.Should().BeTrue();
        result.TotalAlreadyExisted.Should().Be(1);
        result.TotalCreated.Should().Be(0);
        result.Entries[0].Status.Should().Be(SchemaApplyStatus.AlreadyExists);
        result.Entries[0].ErrorCode.Should().Be("-2035");
    }

    [Fact]
    public async Task ApplyAsync_AlreadyExistsWithFlagFalse_CountsAsFailed()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "noObject")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor
        {
            TableHook = _ => new SapObjectAlreadyExistsException("JCA_DLC_RULE", "-2035", "exists")
        };

        var result = await _installer.ApplyAsync(plan, schema, executor,
            new ApplyOptions { TreatAlreadyExistsAsSuccess = false });

        result.IsSuccessful.Should().BeFalse();
        result.TotalFailed.Should().Be(1);
        result.Entries[0].Status.Should().Be(SchemaApplyStatus.Failed);
    }

    [Fact]
    public async Task ApplyAsync_OnEventFires_ForKeyTransitions()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "noObject")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var events = new List<string>();
        var executor = new InMemorySchemaExecutor();

        await _installer.ApplyAsync(plan, schema, executor,
            new ApplyOptions { OnEvent = events.Add });

        events.Should().Contain(e => e.StartsWith("ApplyStarted"));
        events.Should().Contain(e => e.Contains("Creating UserTable 'JCA_DLC_RULE'"));
        events.Should().Contain(e => e.Contains("Created UserTable 'JCA_DLC_RULE'"));
        events.Should().Contain("ApplyFinished: success.");
    }

    [Fact]
    public async Task ApplyAsync_NullPlan_Throws()
    {
        var schema = BuildSchema();
        var act = async () => await _installer.ApplyAsync(null!, schema, new InMemorySchemaExecutor());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ApplyAsync_NullSchema_Throws()
    {
        var plan = new InstallPlan(Array.Empty<InstallPlanEntry>());
        var act = async () => await _installer.ApplyAsync(plan, null!, new InMemorySchemaExecutor());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ApplyAsync_NullExecutor_Throws()
    {
        var schema = BuildSchema();
        var plan = new InstallPlan(Array.Empty<InstallPlanEntry>());
        var act = async () => await _installer.ApplyAsync(plan, schema, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
