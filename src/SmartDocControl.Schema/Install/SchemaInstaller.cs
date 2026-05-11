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
