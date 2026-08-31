using Microsoft.JSInterop;

namespace SmartField.Client.Auth;

public sealed class SessionStorageTokenStore : ITokenStore
{
    private const string TokenKey = "smartfield.auth.token";
    private readonly IJSRuntime jsRuntime;

    public SessionStorageTokenStore(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
    }

    public ValueTask<string?> GetTokenAsync()
    {
        return jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
    }

    public ValueTask SetTokenAsync(string token)
    {
        return jsRuntime.InvokeVoidAsync("sessionStorage.setItem", TokenKey, token);
    }

    public ValueTask ClearTokenAsync()
    {
        return jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
    }
}
