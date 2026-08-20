using ClimateAlert.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ClimateAlert.Application.Tests;

public sealed class ApiRouteTests
{
    [Fact]
    public void AlertRuleEndpointsAreExposed()
    {
        Assert.Equal("api/alert-rules", RouteOf<AlertRulesController>());
        AssertMethod<AlertRulesController>(nameof(AlertRulesController.GetAll), typeof(HttpGetAttribute), null);
        AssertMethod<AlertRulesController>(nameof(AlertRulesController.GetById), typeof(HttpGetAttribute), "{id:guid}");
        AssertMethod<AlertRulesController>(nameof(AlertRulesController.Create), typeof(HttpPostAttribute), null);
        AssertMethod<AlertRulesController>(nameof(AlertRulesController.ChangeStatus), typeof(HttpPatchAttribute), "{id:guid}/status");
    }

    [Fact]
    public void AlertEndpointsAreExposed()
    {
        Assert.Equal("api/alerts", RouteOf<AlertsController>());
        AssertMethod<AlertsController>(nameof(AlertsController.GetAll), typeof(HttpGetAttribute), null);
        AssertMethod<AlertsController>(nameof(AlertsController.GetById), typeof(HttpGetAttribute), "{id:guid}");
        AssertMethod<AlertsController>(nameof(AlertsController.GetByCommunity), typeof(HttpGetAttribute), "/api/communities/{communityId:guid}/alerts");
    }

    private static string? RouteOf<TController>() =>
        typeof(TController).GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>().Single().Template;

    private static void AssertMethod<TController>(string name, Type attributeType, string? template)
    {
        object attribute = typeof(TController).GetMethod(name)!
            .GetCustomAttributes(attributeType, false).Single();
        Assert.Equal(template, ((HttpMethodAttribute)attribute).Template);
    }
}
