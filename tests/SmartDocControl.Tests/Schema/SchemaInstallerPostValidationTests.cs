using FluentAssertions;
using SmartDocControl.Schema.Install;
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
