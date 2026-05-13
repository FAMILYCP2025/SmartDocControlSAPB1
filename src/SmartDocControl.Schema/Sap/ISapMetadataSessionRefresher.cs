namespace SmartDocControl.Schema.Sap;

public interface ISapMetadataSessionRefresher
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
