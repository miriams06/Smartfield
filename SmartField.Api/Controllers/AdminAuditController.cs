using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Application.Audit;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Policy = SmartFieldPolicies.Backoffice)]
public sealed class AdminAuditController : ControllerBase
{
    private readonly IAuditService auditService;

    public AdminAuditController(IAuditService auditService)
    {
        this.auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> Get(
        CancellationToken cancellationToken)
    {
        var audit = await auditService.GetAsync(cancellationToken);
        return Ok(audit);
    }
}
