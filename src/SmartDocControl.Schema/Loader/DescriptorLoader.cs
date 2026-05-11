using System.Text.Json;
using SmartDocControl.Schema.Descriptors;

namespace SmartDocControl.Schema.Loader;

public sealed class DescriptorLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LoadedSchema Load(string schemaFolder)
    {
        if (!Directory.Exists(schemaFolder))
            throw new DirectoryNotFoundException($"Schema folder not found: '{schemaFolder}'.");

        var manifestPath = Path.Combine(schemaFolder, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException(
                $"manifest.json not found in '{schemaFolder}'.", manifestPath);

        var manifestJson = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<SchemaManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize manifest.json.");

        var userTables = new List<UdtDescriptor>();
        var userFields = new List<UdfDescriptor>();

        foreach (var step in manifest.Steps)
        {
            var stepPath = Path.Combine(schemaFolder, step);
            if (!File.Exists(stepPath))
                throw new FileNotFoundException(
                    $"Step file '{step}' referenced in manifest not found in '{schemaFolder}'.", stepPath);

            var json = File.ReadAllText(stepPath);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp))
                throw new InvalidOperationException(
                    $"Step file '{step}' is missing the required 'type' property.");

            var type = typeProp.GetString() ?? string.Empty;

            switch (type)
            {
                case "UserTable":
                    var udt = JsonSerializer.Deserialize<UdtDescriptor>(json, JsonOptions)
                        ?? throw new InvalidOperationException(
                            $"Failed to deserialize '{step}' as UdtDescriptor.");
                    userTables.Add(udt);
                    break;

                case "UserFields":
                    var fileContent = JsonSerializer.Deserialize<UdfFileContent>(json, JsonOptions)
                        ?? throw new InvalidOperationException(
                            $"Failed to deserialize '{step}' as UserFields.");
                    foreach (var field in fileContent.Fields)
                    {
                        userFields.Add(new UdfDescriptor
                        {
                            TableName = fileContent.TableName,
                            Name = field.Name,
                            FieldDescription = field.FieldDescription,
                            Type = field.Type,
                            Size = field.Size,
                            DefaultValue = field.DefaultValue,
                            Mandatory = field.Mandatory,
                            ValidValues = field.ValidValues
                        });
                    }
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Step file '{step}' has unsupported type '{type}'. " +
                        "Supported: UserTable, UserFields.");
            }
        }

        return new LoadedSchema(manifest, userTables, userFields);
    }

    private sealed class UdfFileContent
    {
        public string Type { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public List<UdfFieldEntry> Fields { get; set; } = new();
    }

    private sealed class UdfFieldEntry
    {
        public string Name { get; set; } = string.Empty;
        public string FieldDescription { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? Size { get; set; }
        public string? DefaultValue { get; set; }
        public bool? Mandatory { get; set; }
        public List<ValidValueDescriptor>? ValidValues { get; set; }
    }
}
