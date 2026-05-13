using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SchemaInstallerPostValidationTests
{
    private readonly SchemaInstaller _installer = new();

    [Fact]
    public async Task VerifyAppliedAsync_AllPresent_IsValid()
    {
        var applyResult = new SchemaApplyResult(new[]
        {
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable,
                ObjectName = "JCA_DLC_RULE",
                Status = SchemaApplyStatus.Created
            },
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserField,
                ObjectName = "JCA_DLC_RULE.U_Active",
                Status = SchemaApplyStatus.Created
            }
        });
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });
        metadata.AddField(new SapFieldMetadata
            { TableName = "JCA_DLC_RULE", FieldName = "Active", Type = "db_Alpha", Size = 1 });

        var report = await _installer.VerifyAppliedAsync(applyResult, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeTrue();
        report.VerifiedCount.Should().Be(2);
        report.Missing.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyAppliedAsync_OneMissing_ReturnsMissing()
    {
        var applyResult = new SchemaApplyResult(new[]
        {
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable,
                ObjectName = "JCA_DLC_RULE",
                Status = SchemaApplyStatus.Created
            },
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable,
                ObjectName = "JCA_DLC_EXC",
                Status = SchemaApplyStatus.Created
            }
        });
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });
        // JCA_DLC_EXC intentionally NOT registered → should appear in Missing.

        var report = await _installer.VerifyAppliedAsync(applyResult, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeFalse();
        report.VerifiedCount.Should().Be(1);
        report.Missing.Should().ContainSingle()
            .Which.ObjectName.Should().Be("JCA_DLC_EXC");
    }

    [Fact]
    public async Task VerifyAppliedAsync_OnlyVerifiesCreatedAndAlreadyExists()
    {
        // Skipped, DryRun, Failed, Aborted entries must NOT be re-queried.
        // To prove that, we register zero metadata for those names. If the verifier
        // queried them, they'd appear as Missing.
        var applyResult = new SchemaApplyResult(new[]
        {
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable, ObjectName = "JCA_DLC_SKIP",
                Status = SchemaApplyStatus.Skipped
            },
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable, ObjectName = "JCA_DLC_DRY",
                Status = SchemaApplyStatus.DryRun
            },
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable, ObjectName = "JCA_DLC_FAIL",
                Status = SchemaApplyStatus.Failed
            },
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable, ObjectName = "JCA_DLC_ABRT",
                Status = SchemaApplyStatus.Aborted
            },
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable, ObjectName = "JCA_DLC_PRESENT",
                Status = SchemaApplyStatus.AlreadyExists
            },
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable, ObjectName = "JCA_DLC_NEW",
                Status = SchemaApplyStatus.Created
            }
        });
        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_PRESENT", TableType = "bott_NoObject" });
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_NEW", TableType = "bott_NoObject" });

        var report = await _installer.VerifyAppliedAsync(applyResult, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeTrue();
        report.VerifiedCount.Should().Be(2);
        report.Missing.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyAppliedAsync_TransientNotFound_RetriesOnce()
    {
        var applyResult = new SchemaApplyResult(new[]
        {
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable,
                ObjectName = "JCA_DLC_RULE",
                Status = SchemaApplyStatus.Created
            }
        });

        var metadata = new FlakyInMemorySapMetadata(failuresBeforeSuccess: 1);
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });

        var report = await _installer.VerifyAppliedAsync(applyResult, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeTrue();
        report.VerifiedCount.Should().Be(1);
        metadata.TableCalls.Should().Be(2); // 1st returned null, 2nd returned the table.
    }

    [Fact]
    public async Task VerifyAppliedAsync_PersistentNotFound_AfterRetry_StillMissing()
    {
        var applyResult = new SchemaApplyResult(new[]
        {
            new SchemaApplyEntryResult
            {
                ObjectType = InstallObjectType.UserTable,
                ObjectName = "JCA_DLC_RULE",
                Status = SchemaApplyStatus.Created
            }
        });

        // Inner metadata is empty AND the flaky wrapper would only start
        // returning data after 5 calls — so within 2 attempts it stays missing.
        var metadata = new FlakyInMemorySapMetadata(failuresBeforeSuccess: 5);
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });

        var report = await _installer.VerifyAppliedAsync(applyResult, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeFalse();
        report.Missing.Should().ContainSingle().Which.ObjectName.Should().Be("JCA_DLC_RULE");
        metadata.TableCalls.Should().Be(2); // exactly two attempts, no more.
    }

    [Fact]
    public async Task VerifyAppliedAsync_NullApplyResult_Throws()
    {
        var metadata = new InMemorySapMetadata();
        var act = async () => await _installer.VerifyAppliedAsync(null!, metadata);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task VerifyAppliedAsync_NullMetadata_Throws()
    {
        var applyResult = new SchemaApplyResult(Array.Empty<SchemaApplyEntryResult>());
        var act = async () => await _installer.VerifyAppliedAsync(applyResult, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ─── VerifySchemaAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task VerifySchemaAsync_AllRequiredObjectsPresent_IsValid()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE"), Udt("JCA_DLC_EXC")],
            udfs: [Udf("JCA_DLC_RULE", "Active"), Udf("JCA_DLC_RULE", "MaxDocs")]);

        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_EXC",  TableType = "bott_NoObject" });
        metadata.AddField(new SapFieldMetadata { TableName = "JCA_DLC_RULE", FieldName = "Active", Type = "db_Alpha", Size = 1 });
        metadata.AddField(new SapFieldMetadata { TableName = "JCA_DLC_RULE", FieldName = "MaxDocs", Type = "db_Alpha", Size = 10 });

        var report = await _installer.VerifySchemaAsync(schema, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeTrue();
        report.RequiredCount.Should().Be(4);
        report.VerifiedCount.Should().Be(4);
        report.Missing.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifySchemaAsync_MissingUdt_ReturnsMissing()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE"), Udt("JCA_DLC_EXC")],
            udfs: []);

        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });
        // JCA_DLC_EXC intentionally absent.

        var report = await _installer.VerifySchemaAsync(schema, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeFalse();
        report.RequiredCount.Should().Be(2);
        report.VerifiedCount.Should().Be(1);
        report.Missing.Should().ContainSingle()
            .Which.Should().Match<MissingObject>(m =>
                m.ObjectType == InstallObjectType.UserTable &&
                m.ObjectName == "JCA_DLC_EXC");
    }

    [Fact]
    public async Task VerifySchemaAsync_MissingUdf_ReturnsMissing()
    {
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE")],
            udfs: [Udf("JCA_DLC_RULE", "Active")]);

        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });
        // Field "Active" intentionally absent.

        var report = await _installer.VerifySchemaAsync(schema, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeFalse();
        report.RequiredCount.Should().Be(2);
        report.VerifiedCount.Should().Be(1);
        report.Missing.Should().ContainSingle()
            .Which.Should().Match<MissingObject>(m =>
                m.ObjectType == InstallObjectType.UserField &&
                m.ObjectName == "JCA_DLC_RULE.U_Active");
    }

    [Fact]
    public async Task VerifySchemaAsync_ValidatesSkippedObjectsToo()
    {
        // All objects are ALREADY in SAP, so a real apply would SKIP everything.
        // VerifySchemaAsync must still query and confirm all required objects.
        var schema = BuildSchema(
            udts: [Udt("JCA_DLC_RULE")],
            udfs: [Udf("JCA_DLC_RULE", "Active")]);

        var metadata = new InMemorySapMetadata();
        metadata.AddTable(new SapTableMetadata { TableName = "JCA_DLC_RULE", TableType = "bott_NoObject" });
        metadata.AddField(new SapFieldMetadata { TableName = "JCA_DLC_RULE", FieldName = "Active", Type = "db_Alpha", Size = 1 });

        // No ApplyResult involved — VerifySchemaAsync works from LoadedSchema alone.
        var report = await _installer.VerifySchemaAsync(schema, metadata, TimeSpan.Zero);

        report.IsValid.Should().BeTrue();
        report.RequiredCount.Should().Be(2);
        report.VerifiedCount.Should().Be(2);
        report.Missing.Should().BeEmpty();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

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

    // ─── Flaky stub for retry test ────────────────────────────────────────────

    private sealed class FlakyInMemorySapMetadata : ISapMetadataProvider
    {
        private readonly InMemorySapMetadata _inner = new();
        private readonly int _failuresBeforeSuccess;

        public int TableCalls { get; private set; }
        public int FieldCalls { get; private set; }

        public FlakyInMemorySapMetadata(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public void AddTable(SapTableMetadata t) => _inner.AddTable(t);
        public void AddField(SapFieldMetadata f) => _inner.AddField(f);

        public async Task<SapTableMetadata?> GetTableAsync(string tableName)
        {
            TableCalls++;
            if (TableCalls <= _failuresBeforeSuccess) return null;
            return await _inner.GetTableAsync(tableName);
        }

        public async Task<SapFieldMetadata?> GetFieldAsync(string tableName, string fieldName)
        {
            FieldCalls++;
            if (FieldCalls <= _failuresBeforeSuccess) return null;
            return await _inner.GetFieldAsync(tableName, fieldName);
        }
    }
}
