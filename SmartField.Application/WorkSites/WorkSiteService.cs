using SmartField.Application.Abstractions;
using SmartField.Domain.Entities;

namespace SmartField.Application.WorkSites;

public sealed class WorkSiteService : IWorkSiteService
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 200;
    private const int AddressMaxLength = 500;
    private const int ErpCostCenterCodeMaxLength = 100;
    private const int SearchMaxLength = 200;

    private readonly IWorkSiteStore workSiteStore;
    private readonly ICurrentCompanyProvider currentCompanyProvider;
    private readonly TimeProvider timeProvider;

    public WorkSiteService(
        IWorkSiteStore workSiteStore,
        ICurrentCompanyProvider currentCompanyProvider,
        TimeProvider timeProvider)
    {
        this.workSiteStore = workSiteStore;
        this.currentCompanyProvider = currentCompanyProvider;
        this.timeProvider = timeProvider;
    }

    public async Task<WorkSiteResult<IReadOnlyList<WorkSiteDto>>> SearchAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return WorkSiteResult<IReadOnlyList<WorkSiteDto>>.Failure(
                WorkSiteError.CompanyUnavailable);
        }

        var normalizedSearch = NormalizeOptional(search);
        if (normalizedSearch is { Length: > SearchMaxLength })
        {
            normalizedSearch = normalizedSearch[..SearchMaxLength];
        }

        var workSites = await workSiteStore.SearchAsync(
            companyId.Value,
            normalizedSearch,
            cancellationToken);

        return WorkSiteResult<IReadOnlyList<WorkSiteDto>>.Success(workSites);
    }

    public async Task<WorkSiteResult<WorkSiteDto>> GetAsync(
        Guid workSiteId,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.CompanyUnavailable);
        }

        var workSite = await workSiteStore.GetAsync(
            companyId.Value,
            workSiteId,
            cancellationToken);

        return workSite is null
            ? WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.NotFound)
            : WorkSiteResult<WorkSiteDto>.Success(workSite);
    }

    public async Task<WorkSiteResult<WorkSiteDto>> CreateAsync(
        CreateWorkSiteRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.CompanyUnavailable);
        }

        var validation = ValidateAndNormalize(
            request.Code,
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.GeofenceRadiusMeters,
            request.IsActive,
            request.ErpCostCenterCode);

        if (validation.Errors.Count > 0)
        {
            return WorkSiteResult<WorkSiteDto>.Invalid(validation.Errors);
        }

        if (await workSiteStore.CodeExistsAsync(
            companyId.Value,
            validation.Input.Code,
            null,
            cancellationToken))
        {
            return WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.CodeConflict);
        }

        var workSite = new WorkSite
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId.Value,
            Code = validation.Input.Code,
            Name = validation.Input.Name,
            Address = validation.Input.Address,
            Latitude = validation.Input.Latitude,
            Longitude = validation.Input.Longitude,
            GeofenceRadiusMeters = validation.Input.GeofenceRadiusMeters,
            IsActive = validation.Input.IsActive,
            ErpCostCenterCode = validation.Input.ErpCostCenterCode,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        workSiteStore.Add(workSite);

        try
        {
            await workSiteStore.SaveChangesAsync(cancellationToken);
        }
        catch (WorkSiteCodeConflictException)
        {
            return WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.CodeConflict);
        }

        var createdWorkSite = await workSiteStore.GetAsync(
            companyId.Value,
            workSite.Id,
            cancellationToken);

        return createdWorkSite is null
            ? WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.NotFound)
            : WorkSiteResult<WorkSiteDto>.Success(createdWorkSite);
    }

    public async Task<WorkSiteResult<WorkSiteDto>> UpdateAsync(
        Guid workSiteId,
        UpdateWorkSiteRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.CompanyUnavailable);
        }

        var workSite = await workSiteStore.FindEntityAsync(
            companyId.Value,
            workSiteId,
            cancellationToken);

        if (workSite is null)
        {
            return WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.NotFound);
        }

        var validation = ValidateAndNormalize(
            request.Code,
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.GeofenceRadiusMeters,
            request.IsActive,
            request.ErpCostCenterCode);

        if (validation.Errors.Count > 0)
        {
            return WorkSiteResult<WorkSiteDto>.Invalid(validation.Errors);
        }

        if (await workSiteStore.CodeExistsAsync(
            companyId.Value,
            validation.Input.Code,
            workSiteId,
            cancellationToken))
        {
            return WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.CodeConflict);
        }

        workSite.Code = validation.Input.Code;
        workSite.Name = validation.Input.Name;
        workSite.Address = validation.Input.Address;
        workSite.Latitude = validation.Input.Latitude;
        workSite.Longitude = validation.Input.Longitude;
        workSite.GeofenceRadiusMeters = validation.Input.GeofenceRadiusMeters;
        workSite.IsActive = validation.Input.IsActive;
        workSite.ErpCostCenterCode = validation.Input.ErpCostCenterCode;
        workSite.UpdatedAtUtc = timeProvider.GetUtcNow();

        try
        {
            await workSiteStore.SaveChangesAsync(cancellationToken);
        }
        catch (WorkSiteCodeConflictException)
        {
            return WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.CodeConflict);
        }

        var updatedWorkSite = await workSiteStore.GetAsync(
            companyId.Value,
            workSiteId,
            cancellationToken);

        return updatedWorkSite is null
            ? WorkSiteResult<WorkSiteDto>.Failure(WorkSiteError.NotFound)
            : WorkSiteResult<WorkSiteDto>.Success(updatedWorkSite);
    }

    private static WorkSiteValidationResult ValidateAndNormalize(
        string? code,
        string? name,
        string? address,
        decimal? latitude,
        decimal? longitude,
        int? geofenceRadiusMeters,
        bool isActive,
        string? erpCostCenterCode)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedAddress = NormalizeOptional(address);
        var normalizedErpCostCenterCode = NormalizeOptional(erpCostCenterCode);
        var errors = new Dictionary<string, string[]>();

        ValidateRequiredText(
            normalizedCode,
            CodeMaxLength,
            nameof(CreateWorkSiteRequest.Code),
            "O código",
            errors);
        ValidateRequiredText(
            normalizedName,
            NameMaxLength,
            nameof(CreateWorkSiteRequest.Name),
            "O nome",
            errors);
        ValidateOptionalText(
            normalizedAddress,
            AddressMaxLength,
            nameof(CreateWorkSiteRequest.Address),
            "A morada",
            errors);
        ValidateOptionalText(
            normalizedErpCostCenterCode,
            ErpCostCenterCodeMaxLength,
            nameof(CreateWorkSiteRequest.ErpCostCenterCode),
            "O centro de custo ERP",
            errors);

        if (latitude is < -90 or > 90)
        {
            errors[nameof(CreateWorkSiteRequest.Latitude)] =
                ["A latitude deve estar entre -90 e 90."];
        }

        if (longitude is < -180 or > 180)
        {
            errors[nameof(CreateWorkSiteRequest.Longitude)] =
                ["A longitude deve estar entre -180 e 180."];
        }

        if (geofenceRadiusMeters is <= 0)
        {
            errors[nameof(CreateWorkSiteRequest.GeofenceRadiusMeters)] =
                ["O raio permitido deve ser superior a zero metros."];
        }

        var input = new NormalizedWorkSiteInput(
            normalizedCode,
            normalizedName,
            normalizedAddress,
            latitude,
            longitude,
            geofenceRadiusMeters,
            isActive,
            normalizedErpCostCenterCode);

        return new WorkSiteValidationResult(input, errors);
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

    private sealed record NormalizedWorkSiteInput(
        string Code,
        string Name,
        string? Address,
        decimal? Latitude,
        decimal? Longitude,
        int? GeofenceRadiusMeters,
        bool IsActive,
        string? ErpCostCenterCode);

    private sealed record WorkSiteValidationResult(
        NormalizedWorkSiteInput Input,
        IReadOnlyDictionary<string, string[]> Errors);
}
