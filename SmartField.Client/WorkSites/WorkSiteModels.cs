using System.ComponentModel.DataAnnotations;
using System.Net;

namespace SmartField.Client.WorkSites;

public sealed record WorkSiteDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    bool IsActive,
    string? ErpCostCenterCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record CreateWorkSiteRequest(
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    bool IsActive,
    string? ErpCostCenterCode);

public sealed record UpdateWorkSiteRequest(
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    bool IsActive,
    string? ErpCostCenterCode);

public sealed class WorkSiteEditorModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "O código é obrigatório.")]
    [StringLength(50, ErrorMessage = "O código não pode exceder 50 caracteres.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(200, ErrorMessage = "O nome não pode exceder 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "A morada não pode exceder 500 caracteres.")]
    public string? Address { get; set; }

    [Range(-90, 90, ErrorMessage = "A latitude deve estar entre -90 e 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "A longitude deve estar entre -180 e 180.")]
    public decimal? Longitude { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "O raio permitido deve ser superior a zero metros.")]
    public int? GeofenceRadiusMeters { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(100, ErrorMessage = "O centro de custo ERP não pode exceder 100 caracteres.")]
    public string? ErpCostCenterCode { get; set; }

    public void Load(WorkSiteDto workSite)
    {
        Id = workSite.Id;
        Code = workSite.Code;
        Name = workSite.Name;
        Address = workSite.Address;
        Latitude = workSite.Latitude;
        Longitude = workSite.Longitude;
        GeofenceRadiusMeters = workSite.GeofenceRadiusMeters;
        IsActive = workSite.IsActive;
        ErpCostCenterCode = workSite.ErpCostCenterCode;
    }

    public CreateWorkSiteRequest ToCreateRequest()
    {
        return new CreateWorkSiteRequest(
            Code,
            Name,
            Address,
            Latitude,
            Longitude,
            GeofenceRadiusMeters,
            IsActive,
            ErpCostCenterCode);
    }

    public UpdateWorkSiteRequest ToUpdateRequest()
    {
        return new UpdateWorkSiteRequest(
            Code,
            Name,
            Address,
            Latitude,
            Longitude,
            GeofenceRadiusMeters,
            IsActive,
            ErpCostCenterCode);
    }
}

public sealed record WorkSiteApiProblemDetails(
    string? Title,
    string? Detail,
    Dictionary<string, string[]>? Errors);

public sealed class WorkSiteApiException : Exception
{
    public WorkSiteApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
