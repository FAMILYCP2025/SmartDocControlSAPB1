using SmartDocControl.Schema.Descriptors;

namespace SmartDocControl.Schema.Loader;

public sealed class DescriptorValidator
{
    private static readonly HashSet<string> KnownSapTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "db_Alpha", "db_Numeric", "db_Float", "db_Date", "db_Memo"
    };

    private static readonly HashSet<string> SizeRequiredTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "db_Alpha", "db_Numeric"
    };

    private static readonly HashSet<string> KnownOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateIfNotExists"
    };

    private static readonly HashSet<string> KnownTableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "noObject"
    };

    private const int MaxTableNameLength = 20;
    private const int MaxTableDescriptionLength = 30;

    public void Validate(UdtDescriptor udt)
    {
        if (string.IsNullOrWhiteSpace(udt.TableName))
            throw new DescriptorValidationException("UDT:<unknown>", "TableName is required.");

        var name = $"UDT:{udt.TableName}";

        if (udt.TableName.Length > MaxTableNameLength)
            throw new DescriptorValidationException(name,
                $"TableName '{udt.TableName}' exceeds the SAP B1 maximum length of {MaxTableNameLength} characters ({udt.TableName.Length}).");

        if (string.IsNullOrWhiteSpace(udt.TableDescription))
            throw new DescriptorValidationException(name, "TableDescription is required.");

        if (udt.TableDescription.Length > MaxTableDescriptionLength)
            throw new DescriptorValidationException(name,
                $"TableDescription '{udt.TableDescription}' exceeds the SAP B1 maximum length of {MaxTableDescriptionLength} characters ({udt.TableDescription.Length}).");

        if (string.IsNullOrWhiteSpace(udt.Operation))
            throw new DescriptorValidationException(name, "Operation is required.");

        if (!KnownOperations.Contains(udt.Operation))
            throw new DescriptorValidationException(name,
                $"Operation '{udt.Operation}' is not supported. Supported: {string.Join(", ", KnownOperations)}.");

        if (string.IsNullOrWhiteSpace(udt.TableType))
            throw new DescriptorValidationException(name, "TableType is required.");

        if (!KnownTableTypes.Contains(udt.TableType))
            throw new DescriptorValidationException(name,
                $"TableType '{udt.TableType}' is not recognized. Supported: {string.Join(", ", KnownTableTypes)}.");
    }

    public void Validate(UdfDescriptor udf)
    {
        if (string.IsNullOrWhiteSpace(udf.TableName))
            throw new DescriptorValidationException("UDF:<unknown>", "TableName is required.");

        if (string.IsNullOrWhiteSpace(udf.Name))
            throw new DescriptorValidationException($"UDF:{udf.TableName}", "Name is required.");

        var label = $"UDF:{udf.TableName}.{udf.Name}";

        if (udf.Name.StartsWith("U_", StringComparison.OrdinalIgnoreCase))
            throw new DescriptorValidationException(label,
                $"Field name '{udf.Name}' must not include the 'U_' prefix — SAP adds it automatically.");

        if (string.IsNullOrWhiteSpace(udf.FieldDescription))
            throw new DescriptorValidationException(label, "FieldDescription is required.");

        if (string.IsNullOrWhiteSpace(udf.Type))
            throw new DescriptorValidationException(label, "Type is required.");

        if (!KnownSapTypes.Contains(udf.Type))
            throw new DescriptorValidationException(label,
                $"SAP field type '{udf.Type}' is not recognized. Supported: {string.Join(", ", KnownSapTypes)}.");

        if (SizeRequiredTypes.Contains(udf.Type) && (udf.Size is null || udf.Size <= 0))
            throw new DescriptorValidationException(label,
                $"Size is required and must be > 0 for {udf.Type} fields.");

        if (udf.ValidValues is { Count: > 0 })
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var vv in udf.ValidValues)
            {
                if (!seen.Add(vv.Value))
                    throw new DescriptorValidationException(label,
                        $"ValidValues contains duplicate value '{vv.Value}'.");
            }
        }
    }

    public void Validate(SchemaManifest manifest)
    {
        const string name = "manifest.json";

        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
            throw new DescriptorValidationException(name, "SchemaVersion is required.");

        if (manifest.Steps is null || manifest.Steps.Count == 0)
            throw new DescriptorValidationException(name, "Steps cannot be empty.");

        foreach (var step in manifest.Steps)
        {
            if (!step.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new DescriptorValidationException(name,
                    $"Step '{step}' must end with .json.");
        }
    }
}
