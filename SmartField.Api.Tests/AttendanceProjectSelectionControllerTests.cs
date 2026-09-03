using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartField.Api.Controllers;
using SmartField.Application.Attendance;
using SmartField.Application.Projects;

namespace SmartField.Api.Tests;

public class AttendanceProjectSelectionControllerTests
{
    private static readonly Guid ProjectId = Guid.Parse("326e3e2c-1ae1-4a0f-a959-2e16904f7d1c");
    private static readonly Guid WorkSiteId = Guid.Parse("8c31c9c1-d49e-4c74-a80b-f94f76e39238");

    [Fact]
    public async Task Punch_DerivesWorkSiteFromSelectedProject()
    {
        var attendanceService = new CapturingAttendanceService();
        var projectService = new FakeProjectService(CreateProject("Active", WorkSiteId));
        var controller = new AttendanceController(
            attendanceService,
            projectService,
            NullLogger<AttendanceController>.Instance);
        var browserSuppliedWorkSite = Guid.NewGuid();

        var result = await controller.Punch(
            CreatePunchRequest(ProjectId, browserSuppliedWorkSite),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(attendanceService.LastPunchRequest);
        Assert.Equal(ProjectId, attendanceService.LastPunchRequest.ProjectId);
        Assert.Equal(WorkSiteId, attendanceService.LastPunchRequest.WorkSiteId);
        Assert.NotEqual(browserSuppliedWorkSite, attendanceService.LastPunchRequest.WorkSiteId);
    }

    [Fact]
    public async Task Punch_RejectsSelectedProjectWithoutWorkSite()
    {
        var attendanceService = new CapturingAttendanceService();
        var projectService = new FakeProjectService(CreateProject("Active", null));
        var controller = new AttendanceController(
            attendanceService,
            projectService,
            NullLogger<AttendanceController>.Instance);

        var result = await controller.Punch(
            CreatePunchRequest(ProjectId, null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(attendanceService.LastPunchRequest);
    }

    [Fact]
    public async Task Punch_RejectsInactiveSelectedProject()
    {
        var attendanceService = new CapturingAttendanceService();
        var projectService = new FakeProjectService(CreateProject("Closed", WorkSiteId));
        var controller = new AttendanceController(
            attendanceService,
            projectService,
            NullLogger<AttendanceController>.Instance);

        var result = await controller.Punch(
            CreatePunchRequest(ProjectId, null),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Null(attendanceService.LastPunchRequest);
    }

    private static AttendancePunchRequest CreatePunchRequest(
        Guid? projectId,
        Guid? workSiteId)
    {
        return new AttendancePunchRequest(
            "ClockIn",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            40.2m,
            -8.4m,
            10m,
            workSiteId,
            projectId);
    }

    private static ProjectDto CreateProject(string status, Guid? workSiteId)
    {
        return new ProjectDto(
            ProjectId,
            "OBR-001",
            "Obra Centro",
            "Construction",
            status,
            "Cliente",
            workSiteId,
            workSiteId.HasValue ? "Centro" : null,
            new DateOnly(2026, 9, 1),
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            null);
    }

    private sealed class FakeProjectService(ProjectDto project) : IProjectService
    {
        public Task<ProjectResult<IReadOnlyList<ProjectDto>>> SearchAsync(
            string? search,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                ProjectResult<IReadOnlyList<ProjectDto>>.Success([project]));
        }

        public Task<ProjectResult<ProjectDto>> GetAsync(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                project.Id == projectId
                    ? ProjectResult<ProjectDto>.Success(project)
                    : ProjectResult<ProjectDto>.Failure(ProjectError.NotFound));
        }

        public Task<ProjectResult<ProjectDto>> CreateAsync(
            CreateProjectRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectResult<ProjectDto>> UpdateAsync(
            Guid projectId,
            UpdateProjectRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CapturingAttendanceService : IAttendanceService
    {
        public AttendancePunchRequest? LastPunchRequest { get; private set; }

        public Task<AttendanceResult<AttendancePunchDto>> PunchAsync(
            AttendancePunchRequest request,
            CancellationToken cancellationToken)
        {
            LastPunchRequest = request;
            var value = new AttendancePunchDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                request.EventType ?? "ClockIn",
                request.ClientEventId,
                DateTimeOffset.UtcNow,
                request.ClientTimestampUtc,
                request.Latitude,
                request.Longitude,
                request.AccuracyMeters,
                request.WorkSiteId,
                request.ProjectId,
                true,
                10m,
                false);

            return Task.FromResult(AttendanceResult<AttendancePunchDto>.Success(value));
        }

        public Task<AttendanceResult<AttendanceStateDto>> GetStateAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AttendanceResult<AttendanceTodayDto>> GetTodayAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AttendanceResult<IReadOnlyList<AttendanceHistoryDayDto>>> GetHistoryAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AttendanceResult<AttendanceDayDetailDto>> GetDayAsync(DateOnly date, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AttendanceResult<AttendanceBackofficeDayDto>> GetBackofficeDayAsync(
            AttendanceBackofficeDayFilter filter,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AttendanceResult<AttendanceBackofficeCsvExportDto>> ExportBackofficeCsvAsync(
            AttendanceBackofficeExportFilter filter,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AttendanceResult<AttendanceBackofficeDayDetailDto>> GetBackofficeDayDetailAsync(
            Guid employeeId,
            DateOnly date,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AttendanceResult<AttendanceCorrectionDto>> CorrectBackofficeEventAsync(
            Guid attendanceEventId,
            AttendanceCorrectionRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
