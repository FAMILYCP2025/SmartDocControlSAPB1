using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;
using Xunit;

namespace SmartDocControl.Tests.Schema;

/// <summary>
/// Tests for the SAP metadata eventual-consistency propagation wait:
/// after a UDT is created, the installer polls until the table is visible
/// before attempting the first UDF POST on that table.
/// </summary>
public sealed class SchemaInstallerPropagationTests
{
    private readonly SchemaInstaller _installer = new();

    [Fact]
    public async Task ApplyAsync_UdfOnNewTable_PollsUntilTableAvailable()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE", "bott_NoObject")],
            udfs: [Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1)]);

        // Both UDT and UDF need creating — plan sees both as Create.
        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        var combined = new CombinedSapMetadataAndExecutor();
        // Table appears on 3rd poll: 2 nulls, then the record.
        combined.EnqueueTableResponse("JCA_DLC_RULE", null);
        combined.EnqueueTableResponse("JCA_DLC_RULE", null);
        combined.EnqueueTableResponse("JCA_DLC_RULE",
            new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });

        var events = new List<string>();
        var opts = new ApplyOptions
        {
            MetadataPropagationPollInterval = TimeSpan.Zero,
            MetadataPropagationTimeout = TimeSpan.FromSeconds(10),
            OnEvent = events.Add
        };

        var result = await _installer.ApplyAsync(plan, schema, combined, opts);

        result.IsSuccessful.Should().BeTrue();
        result.TotalCreated.Should().Be(2); // UDT + UDF
        combined.CreatedFields.Should().ContainSingle().Which.Should().Be("JCA_DLC_RULE.U_Active");

        events.Should().Contain(e => e.Contains("Waiting for SAP metadata propagation for table 'JCA_DLC_RULE'"));
        events.Should().Contain(e => e.Contains("SAP metadata available for table 'JCA_DLC_RULE' after 2 retries"));
    }

    [Fact]
    public async Task ApplyAsync_UdfOnNewTable_PropagationTimeout_UdfFails()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE", "bott_NoObject")],
            udfs: [Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1)]);

        var plan = await _installer.PlanAsync(schema, new InMemorySapMetadata());

        // No table responses enqueued — GetTableAsync always returns null.
        var combined = new CombinedSapMetadataAndExecutor();

        var opts = new ApplyOptions
        {
            MetadataPropagationPollInterval = TimeSpan.Zero,
            MetadataPropagationTimeout = TimeSpan.Zero // instant timeout after first null
        };

        var result = await _installer.ApplyAsync(plan, schema, combined, opts);

        result.IsSuccessful.Should().BeFalse();
        result.TotalCreated.Should().Be(1);  // UDT created successfully
        result.TotalFailed.Should().Be(1);   // UDF failed due to propagation timeout
        combined.CreatedFields.Should().BeEmpty();

        var udfEntry = result.Entries.First(e => e.ObjectType == InstallObjectType.UserField);
        udfEntry.Status.Should().Be(SchemaApplyStatus.Failed);
        udfEntry.Message.Should().Contain("timeout");
        udfEntry.ErrorCode.Should().Be("-2004");
    }

    [Fact]
    public async Task ApplyAsync_UdfOnPreexistingTable_NoPollRequired()
    {
        // UDT already in SAP → plan says Skip; UDF not in SAP → plan says Create.
        var planMetadata = new InMemorySapMetadata();
        planMetadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });

        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE", "bott_NoObject")],
            udfs: [Udf("JCA_DLC_RULE", "Active", "db_Alpha", 1)]);

        var plan = await _installer.PlanAsync(schema, planMetadata);
        plan.Entries.Should().ContainSingle(e =>
            e.ObjectType == InstallObjectType.UserTable && e.Action == InstallAction.Skip);

        // Executor+metadata with instant timeout: if polling were triggered it would
        // immediately throw, failing the UDF. Success proves no polling occurred.
        var combined = new CombinedSapMetadataAndExecutor();
        var opts = new ApplyOptions
        {
            MetadataPropagationPollInterval = TimeSpan.Zero,
            MetadataPropagationTimeout = TimeSpan.Zero
        };

        var result = await _installer.ApplyAsync(plan, schema, combined, opts);

        result.IsSuccessful.Should().BeTrue();
        result.TotalSkipped.Should().Be(1);  // UDT skipped (pre-existing)
        result.TotalCreated.Should().Be(1);  // UDF created without any propagation wait
        combined.CreatedFields.Should().ContainSingle().Which.Should().Be("JCA_DLC_RULE.U_Active");
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

    // ─── Test double: implements both ISapMetadataProvider and ISchemaExecutor ─

    /// <summary>
    /// Combined executor + metadata provider for propagation tests.
    /// ISapMetadataProvider.GetTableAsync dequeues pre-programmed responses;
    /// ISchemaExecutor write methods record calls for assertion.
    /// </summary>
    private sealed class CombinedSapMetadataAndExecutor : ISapMetadataProvider, ISchemaExecutor
    {
        private readonly Dictionary<string, Queue<SapTableMetadata?>> _tableQueue
            = new(StringComparer.OrdinalIgnoreCase);

        public List<string> CreatedTables { get; } = [];
        public List<string> CreatedFields { get; } = [];

        public void EnqueueTableResponse(string tableName, SapTableMetadata? response)
        {
            if (!_tableQueue.ContainsKey(tableName))
                _tableQueue[tableName] = new Queue<SapTableMetadata?>();
            _tableQueue[tableName].Enqueue(response);
        }

        public Task<SapTableMetadata?> GetTableAsync(string tableName)
        {
            if (_tableQueue.TryGetValue(tableName, out var q) && q.Count > 0)
                return Task.FromResult(q.Dequeue());
            return Task.FromResult<SapTableMetadata?>(null);
        }

        public Task<SapFieldMetadata?> GetFieldAsync(string tableName, string fieldName)
            => Task.FromResult<SapFieldMetadata?>(null);

        public Task CreateUserTableAsync(UdtDescriptor udt, CancellationToken ct = default)
        {
            CreatedTables.Add(udt.TableName);
            return Task.CompletedTask;
        }

        public Task CreateUserFieldAsync(UdfDescriptor udf, CancellationToken ct = default)
        {
            CreatedFields.Add($"{udf.TableName}.U_{udf.Name}");
            return Task.CompletedTask;
        }
    }
}
