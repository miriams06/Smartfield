using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Application.Audit;
using SmartField.Application.WorkSites;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/worksites")]
[Authorize(Policy = SmartFieldPolicies.Backoffice)]
public sealed class WorkSitesController : ControllerBase
{
    private readonly IWorkSiteService workSiteService;
    private readonly IAuditService auditService;
    private readonly TimeProvider timeProvider;

    public WorkSitesController(
        IWorkSiteService workSiteService,
        IAuditService auditService,
        TimeProvider timeProvider)
    {
        this.workSiteService = workSiteService;
        this.auditService = auditService;
        this.timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkSiteDto>>> Search(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await workSiteService.SearchAsync(search, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkSiteDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await workSiteService.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<WorkSiteDto>> Create(
        CreateWorkSiteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await workSiteService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        var created = result.Value!;
        await AddAuditAsync(created.Id, "Created", null, JsonSerializer.Serialize(created), cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkSiteDto>> Update(
        Guid id,
        UpdateWorkSiteRequest request,
        CancellationToken cancellationToken)
    {
        var beforeResult = await workSiteService.GetAsync(id, cancellationToken);
        var result = await workSiteService.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        await AddAuditAsync(
            id,
            "Updated",
            beforeResult.IsSuccess ? JsonSerializer.Serialize(beforeResult.Value) : null,
            JsonSerializer.Serialize(result.Value),
            cancellationToken);

        return Ok(result.Value);
    }

    private async Task AddAuditAsync(
        Guid entityId,
        string action,
        string? oldValues,
        string? newValues,
        CancellationToken cancellationToken)
    {
        auditService.Add(
            User.GetRequiredCompanyId(),
            User.GetRequiredUserId(),
            "WorkSite",
            entityId,
            action,
            oldValues,
            newValues,
            timeProvider.GetUtcNow());
        await auditService.SaveChangesAsync(cancellationToken);
    }

    private ActionResult MapFailure<T>(WorkSiteResult<T> result)
        where T : class
    {
        return result.Error switch
        {
            WorkSiteError.CompanyUnavailable => Forbid(),
            WorkSiteError.Validation => BadRequest(new ValidationProblemDetails(
                result.ValidationErrors.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Os dados do local de trabalho não são válidos."
            }),
            WorkSiteError.NotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Local de trabalho não encontrado."
            }),
            WorkSiteError.CodeConflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Código de local já utilizado.",
                Detail = "Já existe um local de trabalho com este código na empresa atual."
            }),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível processar o local de trabalho.")
        };
    }
}
