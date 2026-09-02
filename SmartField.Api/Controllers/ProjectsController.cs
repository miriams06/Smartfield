using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Application.Audit;
using SmartField.Application.Projects;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize(Policy = SmartFieldPolicies.Backoffice)]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectService projectService;
    private readonly IAuditService auditService;
    private readonly TimeProvider timeProvider;

    public ProjectsController(
        IProjectService projectService,
        IAuditService auditService,
        TimeProvider timeProvider)
    {
        this.projectService = projectService;
        this.auditService = auditService;
        this.timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> Search(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await projectService.SearchAsync(search, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await projectService.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await projectService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        var created = result.Value!;
        auditService.Add(
            User.GetRequiredCompanyId(),
            User.GetRequiredUserId(),
            "Project",
            created.Id,
            "Created",
            null,
            JsonSerializer.Serialize(created),
            timeProvider.GetUtcNow());
        await auditService.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await projectService.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    private ActionResult MapFailure<T>(ProjectResult<T> result)
        where T : class
    {
        return result.Error switch
        {
            ProjectError.CompanyUnavailable => Forbid(),
            ProjectError.Validation => BadRequest(new ValidationProblemDetails(
                result.ValidationErrors.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Os dados do projeto não são válidos."
            }),
            ProjectError.NotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Projeto não encontrado."
            }),
            ProjectError.WorkSiteNotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Local de trabalho não encontrado."
            }),
            ProjectError.CodeConflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Código de projeto já utilizado.",
                Detail = "Já existe um projeto com este código na empresa atual."
            }),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível processar o projeto.")
        };
    }
}
