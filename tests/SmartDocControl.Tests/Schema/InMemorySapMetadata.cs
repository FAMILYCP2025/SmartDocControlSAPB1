using SmartDocControl.Schema.Sap;

namespace SmartDocControl.Tests.Schema;

internal sealed class InMemorySapMetadata : ISapMetadataProvider
{
    private readonly Dictionary<string, SapTableMetadata> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SapFieldMetadata> _fields = new(StringComparer.OrdinalIgnoreCase);

    public void AddTable(SapTableMetadata table) =>
        _tables[table.TableName] = table;

    public void AddField(SapFieldMetadata field) =>
        _fields[$"{field.TableName}.{field.FieldName}"] = field;

    public Task<SapTableMetadata?> GetTableAsync(string tableName) =>
        Task.FromResult(_tables.GetValueOrDefault(tableName));

    public Task<SapFieldMetadata?> GetFieldAsync(string tableName, string fieldName) =>
        Task.FromResult(_fields.GetValueOrDefault($"{tableName}.{fieldName}"));
}
