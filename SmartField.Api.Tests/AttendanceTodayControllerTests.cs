using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class AttendanceTodayControllerTests
{
    [Fact]
    public void Controller_ExposesPlannerTodayRoute()
    {
        var method = typeof(AttendanceController)
            .GetMethod(nameof(AttendanceController.GetToday));
        var attribute = method?
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("today", attribute.Template);
    }
}
