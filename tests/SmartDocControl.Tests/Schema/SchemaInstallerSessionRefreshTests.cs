using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SchemaInstallerSessionRefreshTests
{
    private readonly SchemaInstaller _installer = new();

    [Fact]
    public async Task ApplyAsync_WithUdfCreate_CallsRefreshBeforeFirstUdf()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_TABLE")],
            udfs: [Udf("JCA_TABLE", "Field1")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        var refresher = new CountingSessionRefresher();
        var executor = new InMemorySchemaExecutor();
        var opts = new ApplyOptions { SessionRefresher = refresher };

        var result = await _installer.ApplyAsync(plan, schema, executor, opts);

        result.IsSuccessful.Should().BeTrue();
        refresher.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_WithMultipleUdfCreates_CallsRefreshOnce()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_TABLE")],
            udfs: [Udf("JCA_TABLE", "Field1"), Udf("JCA_TABLE", "Field2"), Udf("JCA_TABLE", "Field3")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        var refresher = new CountingSessionRefresher();
        var executor = new InMemorySchemaExecutor();
        var opts = new ApplyOptions { SessionRefresher = refresher };

        await _installer.ApplyAsync(plan, schema, executor, opts);

        refresher.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_UdtOnlyPlan_RefreshNotCalled()
    {
        var schema = BuildSchema(udts: [Udt("JCA_TABLE")], udfs: []);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        var refresher = new CountingSessionRefresher();
        var executor = new InMemorySchemaExecutor();
        var opts = new ApplyOptions { SessionRefresher = refresher };

        await _installer.ApplyAsync(plan, schema, executor, opts);

        refresher.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ApplyAsync_AllSkipped_RefreshNotCalled()
    {
        var existingMeta = new InMemorySapMetadata();
        existingMeta.AddTable(new SapTableMetadata { TableName = "JCA_TABLE", TableType = "bott_NoObject" });
        existingMeta.AddField(new SapFieldMetadata { TableName = "JCA_TABLE", FieldName = "Field1", Type = "db_Alpha", Size = 1 });

        var schema = BuildSchema(udts: [Udt("JCA_TABLE")], udfs: [Udf("JCA_TABLE", "Field1")]);
        var plan = await _installer.PlanAsync(schema, existingMeta);
        plan.Entries.Should().AllSatisfy(e => e.Action.Should().Be(InstallAction.Skip));

        var refresher = new CountingSessionRefresher();
        var executor = new InMemorySchemaExecutor();
        var opts = new ApplyOptions { SessionRefresher = refresher };

        await _installer.ApplyAsync(plan, schema, executor, opts);

        refresher.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ApplyAsync_SessionRefresherIsNull_AppliesNormally()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_TABLE")],
            udfs: [Udf("JCA_TABLE", "Field1")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        var executor = new InMemorySchemaExecutor();
        var opts = new ApplyOptions { SessionRefresher = null };

        var result = await _installer.ApplyAsync(plan, schema, executor, opts);

        result.IsSuccessful.Should().BeTrue();
        executor.CreatedFields.Should().ContainSingle().Which.Should().Be("JCA_TABLE.U_Field1");
    }

    [Fact]
    public async Task ApplyAsync_DryRun_RefreshNotCalled()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_TABLE")],
            udfs: [Udf("JCA_TABLE", "Field1")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        var refresher = new CountingSessionRefresher();
        var executor = new InMemorySchemaExecutor();
        var opts = new ApplyOptions { DryRun = true, SessionRefresher = refresher };

        var result = await _installer.ApplyAsync(plan, schema, executor, opts);

        result.TotalDryRun.Should().BeGreaterThan(0);
        refresher.CallCount.Should().Be(0);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static LoadedSchema BuildSchema(
        UdtDescriptor[]? udts = null,
        UdfDescriptor[]? udfs = null) =>
        new(
            new SchemaManifest { SchemaVersion = "1.0.0", Steps = Array.Empty<string>() },
            (IReadOnlyList<UdtDescriptor>)(udts ?? []),
            (IReadOnlyList<UdfDescriptor>)(udfs ?? []));

    private static UdtDescriptor Udt(string name) => new()
    {
        Type = "UserTable", Operation = "CreateIfNotExists",
        TableName = name, TableDescription = name, TableType = "bott_NoObject"
    };

    private static UdfDescriptor Udf(string table, string name) => new()
    {
        TableName = table, Name = name,
        FieldDescription = name, Type = "db_Alpha", Size = 1
    };

    private sealed class CountingSessionRefresher : ISapMetadataSessionRefresher
    {
        public int CallCount { get; private set; }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
