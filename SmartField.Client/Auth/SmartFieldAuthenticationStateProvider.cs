using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace SmartField.Client.Auth;

public sealed class SmartFieldAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly ITokenStore tokenStore;

    public SmartFieldAuthenticationStateProvider(ITokenStore tokenStore)
    {
        this.tokenStore = tokenStore;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await tokenStore.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AnonymousState;
        }

        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Email, ClaimTypes.Role);

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyUserAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void NotifyUserSignedOut()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(AnonymousState));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var segments = jwt.Split('.');
        if (segments.Length < 2)
        {
            return [];
        }

        var payload = DecodeBase64Url(segments[1]);
        using var document = JsonDocument.Parse(payload);
        var claims = new List<Claim>();

        foreach (var property in document.RootElement.EnumerateObject())
        {
            AddClaim(claims, property.Name, property.Value);
        }

        return claims;
    }

    private static void AddClaim(List<Claim> claims, string type, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                AddClaim(claims, type, item);
            }

            return;
        }

        if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        var claimType = type switch
        {
            "email" => ClaimTypes.Email,
            "nameid" or "sub" => ClaimTypes.NameIdentifier,
            "role" => ClaimTypes.Role,
            _ => type
        };

        claims.Add(new Claim(claimType, value.ToString()));
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');

        return Convert.FromBase64String(padded);
    }
}
