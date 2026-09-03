using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Application.Projects;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/attendance/projects")]
[Authorize]
public sealed class AttendanceProjectsController : ControllerBase
{
    private readonly IProjectService projectService;

    public AttendanceProjectsController(IProjectService projectService)
    {
        this.projectService = projectService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttendanceProjectOptionDto>>> GetActive(
        CancellationToken cancellationToken)
    {
        var result = await projectService.SearchAsync(null, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error == ProjectError.CompanyUnavailable
                ? Forbid()
                : Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Não foi possível carregar as obras disponíveis.");
        }

        var projects = result.Value!
            .Where(project => string.Equals(
                project.Status,
                "Active",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Code)
            .Select(project => new AttendanceProjectOptionDto(
                project.Id,
                project.Code,
                project.Name,
                project.WorkSiteId,
                project.WorkSiteName))
            .ToArray();

        return Ok(projects);
    }
}

public sealed record AttendanceProjectOptionDto(
    Guid Id,
    string Code,
    string Name,
    Guid? WorkSiteId,
    string? WorkSiteName);
