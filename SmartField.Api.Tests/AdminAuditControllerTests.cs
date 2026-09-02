using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Authentication;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class AdminAuditControllerTests
{
    [Fact]
    public void Controller_UsesExpectedRouteAndBackofficePolicy()
    {
        var route = typeof(AdminAuditController)
            .GetCustomAttribute<RouteAttribute>();
        var authorize = typeof(AdminAuditController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.Equal("api/admin/audit", route?.Template);
        Assert.Equal(SmartFieldPolicies.Backoffice, authorize?.Policy);
    }

    [Fact]
    public void Get_UsesHttpGet()
    {
        var method = typeof(AdminAuditController)
            .GetMethod(nameof(AdminAuditController.Get));
        var attribute = method?
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Null(attribute.Template);
    }
}
