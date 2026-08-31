namespace SmartField.Client.Auth;

public interface ITokenStore
{
    ValueTask<string?> GetTokenAsync();

    ValueTask SetTokenAsync(string token);

    ValueTask ClearTokenAsync();
}
