using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Infrastructure.Identity;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly IJwtTokenService jwtTokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService)
    {
        this.userManager = userManager;
        this.jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var passwordIsValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordIsValid)
        {
            return Unauthorized();
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        var token = jwtTokenService.CreateToken(user, roles);

        return Ok(new LoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            CreateCurrentUserResponse(user, roles)));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserResponse> Me()
    {
        var roles = User
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();

        return Ok(new CurrentUserResponse(
            User.GetRequiredUserId(),
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            User.GetRequiredCompanyId(),
            User.GetEmployeeId(),
            roles));
    }

    private static CurrentUserResponse CreateCurrentUserResponse(
        ApplicationUser user,
        IReadOnlyCollection<string> roles)
    {
        return new CurrentUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.CompanyId,
            user.EmployeeId,
            roles);
    }
}
