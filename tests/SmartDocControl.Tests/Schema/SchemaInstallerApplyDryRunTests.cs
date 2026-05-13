using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SchemaInstallerApplyDryRunTests
{
    private readonly SchemaInstaller _installer = new();

    [Fact]
    public async Task ApplyAsync_DryRunTrue_CreateEntriesReportDryRunNotCreated()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_RULE", "bott_NoObject"),
                Udt("JCA_DLC_EXC", "bott_NoObject")
            ]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor();

        var result = await _installer.ApplyAsync(plan, schema, executor,
            new ApplyOptions { DryRun = true });

        result.IsSuccessful.Should().BeTrue();
        result.TotalDryRun.Should().Be(2);
        result.TotalCreated.Should().Be(0);
        result.Entries.Should().OnlyContain(e => e.Status == SchemaApplyStatus.DryRun);
    }

    [Fact]
    public async Task ApplyAsync_DryRunTrue_ExecutorNeverInvoked()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE", "bott_NoObject")],
            udfs: [Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1)]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor();

        await _installer.ApplyAsync(plan, schema, executor, new ApplyOptions { DryRun = true });

        executor.CreatedTables.Should().BeEmpty();
        executor.CreatedFields.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_DryRunTrue_SkipEntriesStillReportSkipped()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "bott_NoObject")]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new() { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });

        var plan = await _installer.PlanAsync(schema, metadata);
        var executor = new InMemorySchemaExecutor();

        var result = await _installer.ApplyAsync(plan, schema, executor,
            new ApplyOptions { DryRun = true });

        result.TotalSkipped.Should().Be(1);
        result.TotalDryRun.Should().Be(0);
    }

    [Fact]
    public async Task ApplyAsync_DryRunTrue_EmitsDryRunEvents()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "bott_NoObject")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var events = new List<string>();

        await _installer.ApplyAsync(plan, schema, new InMemorySchemaExecutor(),
            new ApplyOptions { DryRun = true, OnEvent = events.Add });

        events.Should().Contain(e => e.StartsWith("DryRun: would create"));
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
