namespace SmartDocControl.Schema.Sap;

public interface ISapMetadataProvider
{
    Task<SapTableMetadata?> GetTableAsync(string tableName);
    Task<SapFieldMetadata?> GetFieldAsync(string tableName, string fieldName);
}
