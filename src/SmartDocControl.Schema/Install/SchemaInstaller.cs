using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;

namespace SmartDocControl.Schema.Install;

public sealed class SchemaInstaller
{
    public async Task<InstallPlan> PlanAsync(
        LoadedSchema schema,
        ISapMetadataProvider metadata)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(metadata);

        var entries = new List<InstallPlanEntry>();

        foreach (var udt in schema.UserTables)
            entries.Add(await PlanUdtAsync(udt, metadata));

        foreach (var udf in schema.UserFields)
            entries.Add(await PlanUdfAsync(udf, metadata));

        return new InstallPlan(entries);
    }

    public async Task<SchemaApplyResult> ApplyAsync(
        InstallPlan plan,
        LoadedSchema schema,
        ISchemaExecutor executor,
        ApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(executor);
        options ??= new ApplyOptions();

        if (plan.HasBlockingIssues)
        {
            options.OnEvent?.Invoke(
                $"ApplyAborted: plan has {plan.TotalDrifts} blocking drift(s); no changes attempted.");

            var aborted = plan.Entries
                .Select(e => new SchemaApplyEntryResult
                {
                    ObjectType = e.ObjectType,
                    ObjectName = e.ObjectName,
                    Status     = SchemaApplyStatus.Aborted,
                    Message    = e.Action == InstallAction.Drift
                                    ? $"Aborted due to blocking drift: {e.Reason}"
                                    : "Aborted because another entry in the plan is a blocking drift."
                })
                .ToList();

            return new SchemaApplyResult(aborted, wasAborted: true,
                abortReason: "Plan contains blocking drift(s).");
        }

        options.OnEvent?.Invoke(
            $"ApplyStarted: creates={plan.TotalCreates}, skips={plan.TotalSkips}, dryRun={options.DryRun}.");

        var results = new List<SchemaApplyEntryResult>(plan.Entries.Count);
        var abortRemaining = false;
        string? abortReason = null;

        foreach (var entry in plan.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (abortRemaining)
            {
                results.Add(new SchemaApplyEntryResult
                {
                    ObjectType = entry.ObjectType,
                    ObjectName = entry.ObjectName,
                    Status     = SchemaApplyStatus.Aborted,
                    Message    = "Aborted after previous failure (ContinueOnError=false)."
                });
                continue;
            }

            switch (entry.Action)
            {
                case InstallAction.Skip:
                    results.Add(new SchemaApplyEntryResult
                    {
                        ObjectType = entry.ObjectType,
                        ObjectName = entry.ObjectName,
                        Status     = SchemaApplyStatus.Skipped,
                        Message    = entry.Reason
                    });
                    break;

                case InstallAction.Create:
                    if (options.DryRun)
                    {
                        options.OnEvent?.Invoke($"DryRun: would create {entry.ObjectType} '{entry.ObjectName}'.");
                        results.Add(new SchemaApplyEntryResult
                        {
                            ObjectType = entry.ObjectType,
                            ObjectName = entry.ObjectName,
                            Status     = SchemaApplyStatus.DryRun,
                            Message    = "Dry-run: no changes applied."
                        });
                        break;
                    }

                    var outcome = await ExecuteCreateAsync(entry, schema, executor, options, cancellationToken)
                        .ConfigureAwait(false);
                    results.Add(outcome);

                    if (outcome.Status == SchemaApplyStatus.Failed && !options.ContinueOnError)
                    {
                        abortRemaining = true;
                        abortReason = $"Stopped after failure on '{outcome.ObjectName}': {outcome.Message}";
                        options.OnEvent?.Invoke(abortReason);
                    }
                    break;

                default:
                    results.Add(new SchemaApplyEntryResult
                    {
                        ObjectType = entry.ObjectType,
                        ObjectName = entry.ObjectName,
                        Status     = SchemaApplyStatus.Aborted,
                        Message    = $"Unsupported InstallAction '{entry.Action}'."
                    });
                    abortRemaining = true;
                    abortReason ??= $"Unsupported action '{entry.Action}' on '{entry.ObjectName}'.";
                    break;
            }
        }

        var wasAborted = abortReason is not null;
        if (wasAborted)
            options.OnEvent?.Invoke($"ApplyFinishedAborted: {abortReason}");
        else
            options.OnEvent?.Invoke("ApplyFinished: success.");

        return new SchemaApplyResult(results, wasAborted, abortReason);
    }

    /// <summary>
    /// Re-queries SAP Service Layer for every entry that was actually written or
    /// already existed (Created / AlreadyExists). Entries with other statuses
    /// (Skipped, DryRun, Failed, Aborted) are not verified. A single short retry
    /// tolerates the brief eventual-consistency window between a successful POST
    /// and the metadata becoming visible on subsequent GETs.
    /// </summary>
    public async Task<PostValidationReport> VerifyAppliedAsync(
        SchemaApplyResult applyResult,
        ISapMetadataProvider metadata,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applyResult);
        ArgumentNullException.ThrowIfNull(metadata);

        var delay = retryDelay ?? TimeSpan.FromMilliseconds(500);
        var missing = new List<MissingObject>();
        var verified = 0;

        foreach (var entry in applyResult.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.Status != SchemaApplyStatus.Created &&
                entry.Status != SchemaApplyStatus.AlreadyExists)
                continue;

            var found = await VerifyEntryWithRetryAsync(entry, metadata, delay, cancellationToken)
                .ConfigureAwait(false);

            if (found)
            {
                verified++;
            }
            else
            {
                missing.Add(new MissingObject
                {
                    ObjectType = entry.ObjectType,
                    ObjectName = entry.ObjectName,
                    Reason = $"SAP did not return metadata for {entry.ObjectType} '{entry.ObjectName}' after apply."
                });
            }
        }

        return new PostValidationReport(missing, verified);
    }

    private static async Task<bool> VerifyEntryWithRetryAsync(
        SchemaApplyEntryResult entry,
        ISapMetadataProvider metadata,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (attempt > 0 && retryDelay > TimeSpan.Zero)
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);

            bool found;
            if (entry.ObjectType == InstallObjectType.UserTable)
            {
                var table = await metadata.GetTableAsync(entry.ObjectName).ConfigureAwait(false);
                found = table is not null;
            }
            else
            {
                var (tableName, fieldName) = SplitFieldObjectName(entry.ObjectName);
                var field = await metadata.GetFieldAsync(tableName, fieldName).ConfigureAwait(false);
                found = field is not null;
            }

            if (found) return true;
        }

        return false;
    }

    private static async Task<SchemaApplyEntryResult> ExecuteCreateAsync(
        InstallPlanEntry entry,
        LoadedSchema schema,
        ISchemaExecutor executor,
        ApplyOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            if (entry.ObjectType == InstallObjectType.UserTable)
            {
                var udt = schema.UserTables.FirstOrDefault(t =>
                    string.Equals(t.TableName, entry.ObjectName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"Plan references UserTable '{entry.ObjectName}' but no matching descriptor was found in the loaded schema.");

                options.OnEvent?.Invoke($"Creating UserTable '{entry.ObjectName}'.");
                await executor.CreateUserTableAsync(udt, cancellationToken).ConfigureAwait(false);
                options.OnEvent?.Invoke($"Created UserTable '{entry.ObjectName}'.");
            }
            else
            {
                var (tableName, fieldName) = SplitFieldObjectName(entry.ObjectName);
                var udf = schema.UserFields.FirstOrDefault(f =>
                    string.Equals(f.TableName, tableName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"Plan references UserField '{entry.ObjectName}' but no matching descriptor was found in the loaded schema.");

                options.OnEvent?.Invoke($"Creating UserField '{entry.ObjectName}'.");
                await executor.CreateUserFieldAsync(udf, cancellationToken).ConfigureAwait(false);
                options.OnEvent?.Invoke($"Created UserField '{entry.ObjectName}'.");
            }

            return new SchemaApplyEntryResult
            {
                ObjectType = entry.ObjectType,
                ObjectName = entry.ObjectName,
                Status     = SchemaApplyStatus.Created,
                Message    = "Created successfully."
            };
        }
        catch (SapObjectAlreadyExistsException ex) when (options.TreatAlreadyExistsAsSuccess)
        {
            options.OnEvent?.Invoke($"AlreadyExists: '{entry.ObjectName}' (treated as success).");
            return new SchemaApplyEntryResult
            {
                ObjectType = entry.ObjectType,
                ObjectName = entry.ObjectName,
                Status     = SchemaApplyStatus.AlreadyExists,
                Message    = ex.Message,
                ErrorCode  = ex.ErrorCode
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            options.OnEvent?.Invoke($"Failed: '{entry.ObjectName}': {ex.Message}");
            return new SchemaApplyEntryResult
            {
                ObjectType = entry.ObjectType,
                ObjectName = entry.ObjectName,
                Status     = SchemaApplyStatus.Failed,
                Message    = ex.Message,
                ErrorCode  = (ex as SapMetadataException)?.ErrorCode
            };
        }
    }

    private static (string tableName, string fieldName) SplitFieldObjectName(string objectName)
    {
        var dot = objectName.IndexOf('.');
        if (dot <= 0 || dot >= objectName.Length - 1)
            throw new InvalidOperationException(
                $"UserField ObjectName '{objectName}' is malformed; expected 'TABLE.U_FIELD'.");

        var table = objectName[..dot];
        var fieldWithPrefix = objectName[(dot + 1)..];
        var field = fieldWithPrefix.StartsWith("U_", StringComparison.OrdinalIgnoreCase)
            ? fieldWithPrefix[2..]
            : fieldWithPrefix;
        return (table, field);
    }

    private static async Task<InstallPlanEntry> PlanUdtAsync(
        UdtDescriptor udt, ISapMetadataProvider metadata)
    {
        var existing = await metadata.GetTableAsync(udt.TableName);

        if (existing is null)
            return new InstallPlanEntry
            {
                ObjectType  = InstallObjectType.UserTable,
                ObjectName  = udt.TableName,
                Action      = InstallAction.Create,
                Reason      = $"Table '@{udt.TableName}' does not exist in SAP.",
                IsBlocking  = false
            };

        if (!string.Equals(existing.TableType, udt.TableType, StringComparison.OrdinalIgnoreCase))
            return new InstallPlanEntry
            {
                ObjectType  = InstallObjectType.UserTable,
                ObjectName  = udt.TableName,
                Action      = InstallAction.Drift,
                Reason      = $"Table '@{udt.TableName}' exists but TableType differs: " +
                              $"expected '{udt.TableType}', found '{existing.TableType}'.",
                IsBlocking  = true
            };

        return new InstallPlanEntry
        {
            ObjectType  = InstallObjectType.UserTable,
            ObjectName  = udt.TableName,
            Action      = InstallAction.Skip,
            Reason      = $"Table '@{udt.TableName}' already exists and is compatible.",
            IsBlocking  = false
        };
    }

    private static async Task<InstallPlanEntry> PlanUdfAsync(
        UdfDescriptor udf, ISapMetadataProvider metadata)
    {
        var existing = await metadata.GetFieldAsync(udf.TableName, udf.Name);

        if (existing is null)
            return new InstallPlanEntry
            {
                ObjectType  = InstallObjectType.UserField,
                ObjectName  = $"{udf.TableName}.U_{udf.Name}",
                Action      = InstallAction.Create,
                Reason      = $"Field 'U_{udf.Name}' does not exist on '@{udf.TableName}'.",
                IsBlocking  = false
            };

        if (!string.Equals(existing.Type, udf.Type, StringComparison.OrdinalIgnoreCase))
            return new InstallPlanEntry
            {
                ObjectType  = InstallObjectType.UserField,
                ObjectName  = $"{udf.TableName}.U_{udf.Name}",
                Action      = InstallAction.Drift,
                Reason      = $"Field 'U_{udf.Name}' on '@{udf.TableName}' has incompatible type: " +
                              $"expected '{udf.Type}', found '{existing.Type}'.",
                IsBlocking  = true
            };

        var requiredSize = udf.Size ?? 0;
        if (existing.Size < requiredSize)
            return new InstallPlanEntry
            {
                ObjectType  = InstallObjectType.UserField,
                ObjectName  = $"{udf.TableName}.U_{udf.Name}",
                Action      = InstallAction.Drift,
                Reason      = $"Field 'U_{udf.Name}' on '@{udf.TableName}' has insufficient size: " +
                              $"required {requiredSize}, found {existing.Size}.",
                IsBlocking  = true
            };

        return new InstallPlanEntry
        {
            ObjectType  = InstallObjectType.UserField,
            ObjectName  = $"{udf.TableName}.U_{udf.Name}",
            Action      = InstallAction.Skip,
            Reason      = $"Field 'U_{udf.Name}' on '@{udf.TableName}' already exists and is compatible.",
            IsBlocking  = false
        };
    }
}
