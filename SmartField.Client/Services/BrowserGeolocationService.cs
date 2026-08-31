using Microsoft.JSInterop;
using SmartField.Client.Geolocation;

namespace SmartField.Client.Services;

public sealed class BrowserGeolocationService
{
    private readonly IJSRuntime jsRuntime;

    public BrowserGeolocationService(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
    }

    public async ValueTask<BrowserGeolocationResult> GetCurrentPositionAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await jsRuntime.InvokeAsync<BrowserGeolocationResult>(
                "smartFieldGeolocation.getCurrentPosition",
                cancellationToken);
        }
        catch (JSException exception)
        {
            return new BrowserGeolocationResult(
                BrowserGeolocationStatus.UnknownError,
                null,
                null,
                null,
                exception.Message);
        }
    }
}
