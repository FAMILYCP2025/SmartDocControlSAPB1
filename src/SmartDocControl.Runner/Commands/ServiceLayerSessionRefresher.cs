using SmartDocControl.Infrastructure.ServiceLayer;
using SmartDocControl.Schema.Sap;

namespace SmartDocControl.Runner.Commands;

internal sealed class ServiceLayerSessionRefresher : ISapMetadataSessionRefresher
{
    private readonly ServiceLayerClient _slClient;

    public ServiceLayerSessionRefresher(ServiceLayerClient slClient)
    {
        ArgumentNullException.ThrowIfNull(slClient);
        _slClient = slClient;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try { await _slClient.LogoutAsync(cancellationToken).ConfigureAwait(false); }
        catch { /* tolerate logout failure; session may already be expired */ }

        await _slClient.LoginAsync(cancellationToken).ConfigureAwait(false);
    }
}
