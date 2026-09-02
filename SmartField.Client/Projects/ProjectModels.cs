using System.ComponentModel.DataAnnotations;
using System.Net;

namespace SmartField.Client.Projects;

public sealed record ProjectDto(
    Guid Id,
    string Code,
    string Name,
    string ProjectType,
    string Status,
    string? CustomerName,
    Guid? WorkSiteId,
    string? WorkSiteName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ErpProjectCode,
    string? ErpCostCenterCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record CreateProjectRequest(
    string Code,
    string Name,
    string? ProjectType,
    string? Status,
    string? CustomerName,
    Guid? WorkSiteId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ErpProjectCode,
    string? ErpCostCenterCode);

public sealed record UpdateProjectRequest(
    string Code,
    string Name,
    string? ProjectType,
    string? Status,
    string? CustomerName,
    Guid? WorkSiteId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ErpProjectCode,
    string? ErpCostCenterCode);

public sealed class ProjectEditorModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "O código é obrigatório.")]
    [StringLength(50, ErrorMessage = "O código não pode exceder 50 caracteres.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(200, ErrorMessage = "O nome não pode exceder 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public string ProjectType { get; set; } = "Other";

    public string Status { get; set; } = "Draft";

    [StringLength(200, ErrorMessage = "O cliente não pode exceder 200 caracteres.")]
    public string? CustomerName { get; set; }

    public Guid? WorkSiteId { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [StringLength(100, ErrorMessage = "O código de projeto ERP não pode exceder 100 caracteres.")]
    public string? ErpProjectCode { get; set; }

    [StringLength(100, ErrorMessage = "O centro de custo ERP não pode exceder 100 caracteres.")]
    public string? ErpCostCenterCode { get; set; }

    public void Load(ProjectDto project)
    {
        Id = project.Id;
        Code = project.Code;
        Name = project.Name;
        ProjectType = project.ProjectType;
        Status = project.Status;
        CustomerName = project.CustomerName;
        WorkSiteId = project.WorkSiteId;
        StartDate = project.StartDate;
        EndDate = project.EndDate;
        ErpProjectCode = project.ErpProjectCode;
        ErpCostCenterCode = project.ErpCostCenterCode;
    }

    public CreateProjectRequest ToCreateRequest()
    {
        return new CreateProjectRequest(
            Code,
            Name,
            ProjectType,
            Status,
            CustomerName,
            WorkSiteId,
            StartDate,
            EndDate,
            ErpProjectCode,
            ErpCostCenterCode);
    }

    public UpdateProjectRequest ToUpdateRequest()
    {
        return new UpdateProjectRequest(
            Code,
            Name,
            ProjectType,
            Status,
            CustomerName,
            WorkSiteId,
            StartDate,
            EndDate,
            ErpProjectCode,
            ErpCostCenterCode);
    }
}

public sealed record ProjectApiProblemDetails(
    string? Title,
    string? Detail,
    Dictionary<string, string[]>? Errors);

public sealed class ProjectApiException : Exception
{
    public ProjectApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
