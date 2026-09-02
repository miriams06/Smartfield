using SmartField.Application.Abstractions;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Projects;

public sealed class ProjectService : IProjectService
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 200;
    private const int CustomerNameMaxLength = 200;
    private const int ErpProjectCodeMaxLength = 100;
    private const int ErpCostCenterCodeMaxLength = 100;
    private const int SearchMaxLength = 200;

    private readonly IProjectStore projectStore;
    private readonly ICurrentCompanyProvider currentCompanyProvider;
    private readonly TimeProvider timeProvider;

    public ProjectService(
        IProjectStore projectStore,
        ICurrentCompanyProvider currentCompanyProvider,
        TimeProvider timeProvider)
    {
        this.projectStore = projectStore;
        this.currentCompanyProvider = currentCompanyProvider;
        this.timeProvider = timeProvider;
    }

    public async Task<ProjectResult<IReadOnlyList<ProjectDto>>> SearchAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return ProjectResult<IReadOnlyList<ProjectDto>>.Failure(
                ProjectError.CompanyUnavailable);
        }

        var normalizedSearch = NormalizeOptional(search);
        if (normalizedSearch is { Length: > SearchMaxLength })
        {
            normalizedSearch = normalizedSearch[..SearchMaxLength];
        }

        var projects = await projectStore.SearchAsync(
            companyId.Value,
            normalizedSearch,
            cancellationToken);

        return ProjectResult<IReadOnlyList<ProjectDto>>.Success(projects);
    }

    public async Task<ProjectResult<ProjectDto>> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return ProjectResult<ProjectDto>.Failure(ProjectError.CompanyUnavailable);
        }

        var project = await projectStore.GetAsync(
            companyId.Value,
            projectId,
            cancellationToken);

        return project is null
            ? ProjectResult<ProjectDto>.Failure(ProjectError.NotFound)
            : ProjectResult<ProjectDto>.Success(project);
    }

    public async Task<ProjectResult<ProjectDto>> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return ProjectResult<ProjectDto>.Failure(ProjectError.CompanyUnavailable);
        }

        var validation = ValidateAndNormalize(
            request.Code,
            request.Name,
            request.ProjectType,
            request.Status,
            request.CustomerName,
            request.WorkSiteId,
            request.StartDate,
            request.EndDate,
            request.ErpProjectCode,
            request.ErpCostCenterCode);

        if (validation.Errors.Count > 0)
        {
            return ProjectResult<ProjectDto>.Invalid(validation.Errors);
        }

        var workSiteValidation = await ValidateWorkSiteAsync(
            companyId.Value,
            validation.Input.WorkSiteId,
            cancellationToken);
        if (workSiteValidation != ProjectError.None)
        {
            return ProjectResult<ProjectDto>.Failure(workSiteValidation);
        }

        if (await projectStore.CodeExistsAsync(
            companyId.Value,
            validation.Input.Code,
            null,
            cancellationToken))
        {
            return ProjectResult<ProjectDto>.Failure(ProjectError.CodeConflict);
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId.Value,
            Code = validation.Input.Code,
            Name = validation.Input.Name,
            ProjectType = validation.Input.ProjectType,
            Status = validation.Input.Status,
            CustomerName = validation.Input.CustomerName,
            WorkSiteId = validation.Input.WorkSiteId,
            StartDate = validation.Input.StartDate,
            EndDate = validation.Input.EndDate,
            ErpProjectCode = validation.Input.ErpProjectCode,
            ErpCostCenterCode = validation.Input.ErpCostCenterCode,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        projectStore.Add(project);

        try
        {
            await projectStore.SaveChangesAsync(cancellationToken);
        }
        catch (ProjectCodeConflictException)
        {
            return ProjectResult<ProjectDto>.Failure(ProjectError.CodeConflict);
        }

        var createdProject = await projectStore.GetAsync(
            companyId.Value,
            project.Id,
            cancellationToken);

        return createdProject is null
            ? ProjectResult<ProjectDto>.Failure(ProjectError.NotFound)
            : ProjectResult<ProjectDto>.Success(createdProject);
    }

    public async Task<ProjectResult<ProjectDto>> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return ProjectResult<ProjectDto>.Failure(ProjectError.CompanyUnavailable);
        }

        var project = await projectStore.FindEntityAsync(
            companyId.Value,
            projectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectResult<ProjectDto>.Failure(ProjectError.NotFound);
        }

        var validation = ValidateAndNormalize(
            request.Code,
            request.Name,
            request.ProjectType,
            request.Status,
            request.CustomerName,
            request.WorkSiteId,
            request.StartDate,
            request.EndDate,
            request.ErpProjectCode,
            request.ErpCostCenterCode);

        if (validation.Errors.Count > 0)
        {
            return ProjectResult<ProjectDto>.Invalid(validation.Errors);
        }

        var workSiteValidation = await ValidateWorkSiteAsync(
            companyId.Value,
            validation.Input.WorkSiteId,
            cancellationToken);
        if (workSiteValidation != ProjectError.None)
        {
            return ProjectResult<ProjectDto>.Failure(workSiteValidation);
        }

        if (await projectStore.CodeExistsAsync(
            companyId.Value,
            validation.Input.Code,
            projectId,
            cancellationToken))
        {
            return ProjectResult<ProjectDto>.Failure(ProjectError.CodeConflict);
        }

        project.Code = validation.Input.Code;
        project.Name = validation.Input.Name;
        project.ProjectType = validation.Input.ProjectType;
        project.Status = validation.Input.Status;
        project.CustomerName = validation.Input.CustomerName;
        project.WorkSiteId = validation.Input.WorkSiteId;
        project.StartDate = validation.Input.StartDate;
        project.EndDate = validation.Input.EndDate;
        project.ErpProjectCode = validation.Input.ErpProjectCode;
        project.ErpCostCenterCode = validation.Input.ErpCostCenterCode;
        project.UpdatedAtUtc = timeProvider.GetUtcNow();

        try
        {
            await projectStore.SaveChangesAsync(cancellationToken);
        }
        catch (ProjectCodeConflictException)
        {
            return ProjectResult<ProjectDto>.Failure(ProjectError.CodeConflict);
        }

        var updatedProject = await projectStore.GetAsync(
            companyId.Value,
            projectId,
            cancellationToken);

        return updatedProject is null
            ? ProjectResult<ProjectDto>.Failure(ProjectError.NotFound)
            : ProjectResult<ProjectDto>.Success(updatedProject);
    }

    private async Task<ProjectError> ValidateWorkSiteAsync(
        Guid companyId,
        Guid? workSiteId,
        CancellationToken cancellationToken)
    {
        if (!workSiteId.HasValue)
        {
            return ProjectError.None;
        }

        return await projectStore.WorkSiteExistsAsync(
            companyId,
            workSiteId.Value,
            cancellationToken)
            ? ProjectError.None
            : ProjectError.WorkSiteNotFound;
    }

    private static ProjectValidationResult ValidateAndNormalize(
        string? code,
        string? name,
        string? projectType,
        string? status,
        string? customerName,
        Guid? workSiteId,
        DateOnly? startDate,
        DateOnly? endDate,
        string? erpProjectCode,
        string? erpCostCenterCode)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCustomerName = NormalizeOptional(customerName);
        var normalizedErpProjectCode = NormalizeOptional(erpProjectCode);
        var normalizedErpCostCenterCode = NormalizeOptional(erpCostCenterCode);
        var errors = new Dictionary<string, string[]>();

        ValidateRequiredText(
            normalizedCode,
            CodeMaxLength,
            nameof(CreateProjectRequest.Code),
            "O código",
            errors);
        ValidateRequiredText(
            normalizedName,
            NameMaxLength,
            nameof(CreateProjectRequest.Name),
            "O nome",
            errors);
        ValidateOptionalText(
            normalizedCustomerName,
            CustomerNameMaxLength,
            nameof(CreateProjectRequest.CustomerName),
            "O cliente",
            errors);
        ValidateOptionalText(
            normalizedErpProjectCode,
            ErpProjectCodeMaxLength,
            nameof(CreateProjectRequest.ErpProjectCode),
            "O código de projeto ERP",
            errors);
        ValidateOptionalText(
            normalizedErpCostCenterCode,
            ErpCostCenterCodeMaxLength,
            nameof(CreateProjectRequest.ErpCostCenterCode),
            "O centro de custo ERP",
            errors);

        if (workSiteId == Guid.Empty)
        {
            errors[nameof(CreateProjectRequest.WorkSiteId)] =
                ["O local de trabalho selecionado não é válido."];
        }

        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
        {
            errors[nameof(CreateProjectRequest.EndDate)] =
                ["A data de fim não pode ser anterior à data de início."];
        }

        var normalizedProjectType = ParseEnumOrDefault(
            projectType,
            ProjectType.Other,
            nameof(CreateProjectRequest.ProjectType),
            "O tipo de projeto",
            errors);
        var normalizedStatus = ParseEnumOrDefault(
            status,
            ProjectStatus.Draft,
            nameof(CreateProjectRequest.Status),
            "O estado",
            errors);

        var input = new NormalizedProjectInput(
            normalizedCode,
            normalizedName,
            normalizedProjectType,
            normalizedStatus,
            normalizedCustomerName,
            workSiteId,
            startDate,
            endDate,
            normalizedErpProjectCode,
            normalizedErpCostCenterCode);

        return new ProjectValidationResult(input, errors);
    }

    private static TEnum ParseEnumOrDefault<TEnum>(
        string? value,
        TEnum defaultValue,
        string propertyName,
        string displayName,
        IDictionary<string, string[]> errors)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        errors[propertyName] = [$"{displayName} não é válido."];
        return defaultValue;
    }

    private static void ValidateRequiredText(
        string value,
        int maxLength,
        string propertyName,
        string displayName,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[propertyName] = [$"{displayName} é obrigatório."];
        }
        else if (value.Length > maxLength)
        {
            errors[propertyName] =
                [$"{displayName} não pode exceder {maxLength} caracteres."];
        }
    }

    private static void ValidateOptionalText(
        string? value,
        int maxLength,
        string propertyName,
        string displayName,
        IDictionary<string, string[]> errors)
    {
        if (value is { Length: > 0 } && value.Length > maxLength)
        {
            errors[propertyName] =
                [$"{displayName} não pode exceder {maxLength} caracteres."];
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ProjectValidationResult(
        NormalizedProjectInput Input,
        IReadOnlyDictionary<string, string[]> Errors);
}
