using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Application.WorkSites;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/worksites")]
[Authorize(Policy = SmartFieldPolicies.Backoffice)]
public sealed class WorkSitesController : ControllerBase
{
    private readonly IWorkSiteService workSiteService;

    public WorkSitesController(IWorkSiteService workSiteService)
    {
        this.workSiteService = workSiteService;
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

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value!.Id },
            result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkSiteDto>> Update(
        Guid id,
        UpdateWorkSiteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await workSiteService.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
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
