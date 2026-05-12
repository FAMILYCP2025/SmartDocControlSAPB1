using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SchemaInstallerApplyBlockingTests
{
    private readonly SchemaInstaller _installer = new();

    [Fact]
    public async Task ApplyAsync_PlanHasBlockingDrift_AbortsWithoutCallingExecutor()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_RULE", "noObject")]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "Document" });

        var plan = await _installer.PlanAsync(schema, metadata);
        var executor = new InMemorySchemaExecutor();

        var result = await _installer.ApplyAsync(plan, schema, executor);

        result.WasAborted.Should().BeTrue();
        result.IsSuccessful.Should().BeFalse();
        result.AbortReason.Should().Contain("blocking drift");
        result.Entries.Should().OnlyContain(e => e.Status == SchemaApplyStatus.Aborted);
        executor.CreatedTables.Should().BeEmpty();
        executor.CreatedFields.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_BlockingPlan_MixedEntries_AllReportedAborted()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_NEW", "noObject"),       // would be Create
                Udt("JCA_DLC_DRIFT", "noObject")      // becomes Drift
            ]);
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_DRIFT", TableType = "Document" });

        var plan = await _installer.PlanAsync(schema, metadata);
        var executor = new InMemorySchemaExecutor();

        var result = await _installer.ApplyAsync(plan, schema, executor);

        result.WasAborted.Should().BeTrue();
        result.TotalAborted.Should().Be(2);
        executor.CreatedTables.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_ExecutorThrows_ContinueOnErrorFalse_StopsAfterFirstFailure()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_FAIL", "noObject"),
                Udt("JCA_DLC_NEXT", "noObject"),
                Udt("JCA_DLC_LAST", "noObject")
            ]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        var executor = new InMemorySchemaExecutor
        {
            TableHook = udt => udt.TableName == "JCA_DLC_FAIL"
                ? new SapMetadataException(udt.TableName, 500, "-100", "boom")
                : null
        };

        var result = await _installer.ApplyAsync(plan, schema, executor,
            new ApplyOptions { ContinueOnError = false });

        result.IsSuccessful.Should().BeFalse();
        result.WasAborted.Should().BeTrue();
        result.AbortReason.Should().Contain("JCA_DLC_FAIL");
        result.TotalFailed.Should().Be(1);
        result.TotalAborted.Should().Be(2);
        executor.CreatedTables.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_ExecutorThrows_ContinueOnErrorTrue_ProcessesRemaining()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_FAIL", "noObject"),
                Udt("JCA_DLC_OK", "noObject")
            ]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        var executor = new InMemorySchemaExecutor
        {
            TableHook = udt => udt.TableName == "JCA_DLC_FAIL"
                ? new SapMetadataException(udt.TableName, 500, "-100", "boom")
                : null
        };

        var result = await _installer.ApplyAsync(plan, schema, executor,
            new ApplyOptions { ContinueOnError = true });

        result.IsSuccessful.Should().BeFalse();
        result.WasAborted.Should().BeFalse();
        result.TotalFailed.Should().Be(1);
        result.TotalCreated.Should().Be(1);
        executor.CreatedTables.Should().ContainSingle().Which.Should().Be("JCA_DLC_OK");
    }

    [Fact]
    public async Task ApplyAsync_FailedEntryCarriesErrorCodeFromException()
    {
        var schema = BuildSchema(udts: [Udt("JCA_DLC_FAIL", "noObject")]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        var executor = new InMemorySchemaExecutor
        {
            TableHook = _ => new SapMetadataException("JCA_DLC_FAIL", 400, "-7", "bad request")
        };

        var result = await _installer.ApplyAsync(plan, schema, executor,
            new ApplyOptions { ContinueOnError = true });

        result.Entries[0].Status.Should().Be(SchemaApplyStatus.Failed);
        result.Entries[0].ErrorCode.Should().Be("-7");
    }

    [Fact]
    public async Task ApplyAsync_CancellationRequested_Throws()
    {
        var schema = BuildSchema(
            udts:
            [
                Udt("JCA_DLC_ONE", "noObject"),
                Udt("JCA_DLC_TWO", "noObject")
            ]);
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _installer.ApplyAsync(plan, schema, new InMemorySchemaExecutor(),
            options: null, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
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
}
