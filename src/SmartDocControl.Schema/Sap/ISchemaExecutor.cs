using SmartDocControl.Schema.Descriptors;

namespace SmartDocControl.Schema.Sap;

public interface ISchemaExecutor
{
    Task CreateUserTableAsync(UdtDescriptor udt, CancellationToken cancellationToken = default);

    Task CreateUserFieldAsync(UdfDescriptor udf, CancellationToken cancellationToken = default);
}
