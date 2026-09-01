using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Authentication;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class AttendanceControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var authorize = typeof(AttendanceController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Null(authorize.Policy);
    }

    [Fact]
    public void Controller_ExposesPunchRoute()
    {
        var controllerRoute = typeof(AttendanceController)
            .GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/attendance", controllerRoute?.Template);

        var method = typeof(AttendanceController)
            .GetMethod(nameof(AttendanceController.Punch));
        var attribute = method?
            .GetCustomAttributes(typeof(HttpPostAttribute), inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("punch", attribute.Template);
    }

    [Fact]
    public void Controller_ExposesStateRoute()
    {
        var method = typeof(AttendanceController)
            .GetMethod(nameof(AttendanceController.GetState));
        var attribute = method?
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("state", attribute.Template);
    }

    [Fact]
    public void Controller_ExposesBackofficeDayRouteForBackofficePolicy()
    {
        var method = typeof(AttendanceController)
            .GetMethod(nameof(AttendanceController.GetBackofficeDay));
        var routeAttribute = method?
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();
        var authorizeAttribute = method?
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(routeAttribute);
        Assert.Equal("admin/day", routeAttribute.Template);
        Assert.Equal(SmartFieldPolicies.Backoffice, authorizeAttribute?.Policy);
    }

    [Fact]
    public void Controller_ExposesBackofficeDayDetailRouteForBackofficePolicy()
    {
        var method = typeof(AttendanceController)
            .GetMethod(nameof(AttendanceController.GetBackofficeDayDetail));
        var routeAttribute = method?
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();
        var authorizeAttribute = method?
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(routeAttribute);
        Assert.Equal("admin/day/{date}/employees/{employeeId}", routeAttribute.Template);
        Assert.Equal(SmartFieldPolicies.Backoffice, authorizeAttribute?.Policy);
    }
}
