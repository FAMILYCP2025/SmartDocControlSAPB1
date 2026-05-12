using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Sap;

namespace SmartDocControl.Tests.Schema;

internal sealed class InMemorySchemaExecutor : ISchemaExecutor
{
    public List<string> CreatedTables { get; } = new();
    public List<string> CreatedFields { get; } = new();

    public Func<UdtDescriptor, Exception?>? TableHook { get; set; }
    public Func<UdfDescriptor, Exception?>? FieldHook { get; set; }

    public Task CreateUserTableAsync(UdtDescriptor udt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ex = TableHook?.Invoke(udt);
        if (ex is not null) throw ex;

        CreatedTables.Add(udt.TableName);
        return Task.CompletedTask;
    }

    public Task CreateUserFieldAsync(UdfDescriptor udf, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ex = FieldHook?.Invoke(udf);
        if (ex is not null) throw ex;

        CreatedFields.Add($"{udf.TableName}.U_{udf.Name}");
        return Task.CompletedTask;
    }
}
