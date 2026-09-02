namespace SmartField.Integrations.Primavera;

public sealed class PrimaveraOptions
{
    public const string SectionName = "Primavera";

    public string? BaseUrl { get; init; }

    public string? Company { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? ApiKey { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Company)
        && (!string.IsNullOrWhiteSpace(ApiKey)
            || (!string.IsNullOrWhiteSpace(Username)
                && !string.IsNullOrWhiteSpace(Password)));
}
