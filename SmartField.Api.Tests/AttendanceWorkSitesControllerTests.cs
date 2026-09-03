using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Controllers;
using SmartField.Application.Abstractions;
using SmartField.Application.Employees;
using SmartField.Application.WorkSites;

namespace SmartField.Api.Tests;

public class AttendanceWorkSitesControllerTests
{
    private static readonly Guid EmployeeId = Guid.Parse("fd623125-5d40-4e6c-8265-4ff031d76271");
    private static readonly Guid DefaultWorkSiteId = Guid.Parse("88ad013d-47f3-46c5-aaf6-e80fbfa286f8");

    [Fact]
    public async Task GetActive_ReturnsOnlyActiveWorkSitesAndMarksDefault()
    {
        var workSiteService = new FakeWorkSiteService(
        [
            CreateWorkSite(DefaultWorkSiteId, "DEF", "Local habitual", true),
            CreateWorkSite(Guid.NewGuid(), "ALT", "Local alternativo", true),
            CreateWorkSite(Guid.NewGuid(), "OFF", "Local inativo", false)
        ]);
        var employeeService = new FakeEmployeeService(CreateEmployee(DefaultWorkSiteId));
        var controller = new AttendanceWorkSitesController(
            workSiteService,
            employeeService,
            new FakeCurrentUserProvider());

        var result = await controller.GetActive(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var workSites = Assert.IsAssignableFrom<IReadOnlyList<AttendanceWorkSiteOptionDto>>(ok.Value);
        Assert.Equal(2, workSites.Count);
        Assert.Equal(DefaultWorkSiteId, workSites[0].Id);
        Assert.True(workSites[0].IsDefault);
        Assert.DoesNotContain(workSites, workSite => workSite.Code == "OFF");
    }

    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var authorize = typeof(AttendanceWorkSitesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Null(authorize.Policy);
    }

    private static WorkSiteDto CreateWorkSite(
        Guid id,
        string code,
        string name,
        bool isActive)
    {
        return new WorkSiteDto(
            id,
            code,
            name,
            $"Morada {code}",
            40.2m,
            -8.4m,
            100,
            isActive,
            null,
            DateTimeOffset.UtcNow,
            null);
    }

    private static EmployeeDto CreateEmployee(Guid? defaultWorkSiteId)
    {
        return new EmployeeDto(
            EmployeeId,
            "FUNC001",
            "Funcionário Demo",
            "funcionario@smartfield.local",
            null,
            true,
            defaultWorkSiteId,
            defaultWorkSiteId.HasValue ? "Local habitual" : null,
            Guid.NewGuid(),
            "funcionario@smartfield.local",
            null,
            DateTimeOffset.UtcNow,
            null);
    }

    private sealed class FakeCurrentUserProvider : ICurrentUserProvider
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? EmployeeId => AttendanceWorkSitesControllerTests.EmployeeId;
    }

    private sealed class FakeWorkSiteService(IReadOnlyList<WorkSiteDto> workSites) : IWorkSiteService
    {
        public Task<WorkSiteResult<IReadOnlyList<WorkSiteDto>>> SearchAsync(
            string? search,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                WorkSiteResult<IReadOnlyList<WorkSiteDto>>.Success(workSites));
        }

        public Task<WorkSiteResult<WorkSiteDto>> GetAsync(
            Guid workSiteId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WorkSiteResult<WorkSiteDto>> CreateAsync(
            CreateWorkSiteRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WorkSiteResult<WorkSiteDto>> UpdateAsync(
            Guid workSiteId,
            UpdateWorkSiteRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeEmployeeService(EmployeeDto employee) : IEmployeeService
    {
        public Task<EmployeeResult<EmployeeDto>> GetAsync(
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                employee.Id == employeeId
                    ? EmployeeResult<EmployeeDto>.Success(employee)
                    : EmployeeResult<EmployeeDto>.Failure(EmployeeError.NotFound));
        }

        public Task<EmployeeResult<IReadOnlyList<EmployeeDto>>> SearchAsync(
            string? search,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EmployeeResult<EmployeeOptions>> GetOptionsAsync(
            Guid? employeeId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EmployeeResult<EmployeeDto>> CreateAsync(
            CreateEmployeeRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EmployeeResult<EmployeeDto>> UpdateAsync(
            Guid employeeId,
            UpdateEmployeeRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
