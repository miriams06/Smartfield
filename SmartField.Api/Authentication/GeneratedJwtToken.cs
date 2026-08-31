namespace SmartField.Api.Authentication;

public sealed record GeneratedJwtToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
