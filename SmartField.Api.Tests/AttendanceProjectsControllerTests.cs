using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Controllers;
using SmartField.Application.Projects;

namespace SmartField.Api.Tests;

public class AttendanceProjectsControllerTests
{
    [Fact]
    public async Task GetActive_ReturnsOnlyActiveProjects()
    {
        var service = new FakeProjectService(
        [
            CreateProject("A", "Obra ativa", "Active"),
            CreateProject("B", "Obra fechada", "Closed")
        ]);
        var controller = new AttendanceProjectsController(service);

        var result = await controller.GetActive(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var projects = Assert.IsAssignableFrom<IReadOnlyList<AttendanceProjectOptionDto>>(ok.Value);
        var project = Assert.Single(projects);
        Assert.Equal("A", project.Code);
        Assert.Equal("Obra ativa", project.Name);
    }

    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var authorize = typeof(AttendanceProjectsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Null(authorize.Policy);
    }

    private static ProjectDto CreateProject(string code, string name, string status)
    {
        var workSiteId = Guid.NewGuid();
        return new ProjectDto(
            Guid.NewGuid(),
            code,
            name,
            "Construction",
            status,
            null,
            workSiteId,
            $"Local {code}",
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            null);
    }

    private sealed class FakeProjectService(IReadOnlyList<ProjectDto> projects) : IProjectService
    {
        public Task<ProjectResult<IReadOnlyList<ProjectDto>>> SearchAsync(
            string? search,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ProjectResult<IReadOnlyList<ProjectDto>>.Success(projects));
        }

        public Task<ProjectResult<ProjectDto>> GetAsync(Guid projectId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ProjectResult<ProjectDto>> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ProjectResult<ProjectDto>> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
