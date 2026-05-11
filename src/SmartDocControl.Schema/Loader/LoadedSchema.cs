using SmartDocControl.Schema.Descriptors;

namespace SmartDocControl.Schema.Loader;

public sealed class LoadedSchema
{
    public SchemaManifest Manifest { get; }
    public IReadOnlyList<UdtDescriptor> UserTables { get; }
    public IReadOnlyList<UdfDescriptor> UserFields { get; }

    public LoadedSchema(
        SchemaManifest manifest,
        IReadOnlyList<UdtDescriptor> userTables,
        IReadOnlyList<UdfDescriptor> userFields)
    {
        Manifest = manifest;
        UserTables = userTables;
        UserFields = userFields;
    }
}
