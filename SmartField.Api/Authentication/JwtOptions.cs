namespace SmartField.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SmartField";

    public string Audience { get; set; } = "SmartField.Client";

    public int ExpirationMinutes { get; set; } = 480;

    public string? SigningKey { get; set; }
}
