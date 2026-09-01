using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class AttendanceHistoryControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var authorize = typeof(AttendanceController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
    }

    [Fact]
    public void Controller_ExposesHistoryRoute()
    {
        var method = typeof(AttendanceController)
            .GetMethod(nameof(AttendanceController.GetHistory));
        var attribute = method?
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("history", attribute.Template);
    }

    [Fact]
    public void Controller_ExposesDayDetailRoute()
    {
        var method = typeof(AttendanceController)
            .GetMethod(nameof(AttendanceController.GetDay));
        var attribute = method?
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("day/{date}", attribute.Template);
    }
}
