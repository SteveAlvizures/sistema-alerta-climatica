using ClimateAlert.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Authorization;

namespace ClimateAlert.Application.Tests;

public sealed class ApiRouteTests
{
    [Fact]
    public void AuthenticationEndpointIsExposed()
    {
        Assert.Equal("api/auth", RouteOf<AuthController>());
        AssertMethod<AuthController>(nameof(AuthController.Login), typeof(HttpPostAttribute), "login");
    }

    [Fact]
    public void AdministrativeWritesRequireAdministratorRole()
    {
        AssertProtected<CommunitiesController>(nameof(CommunitiesController.Create));
        AssertProtected<CommunitiesController>(nameof(CommunitiesController.Update));
        AssertProtected<CommunitiesController>(nameof(CommunitiesController.Delete));
        AssertProtected<SensorsController>(nameof(SensorsController.Create));
        AssertProtected<SensorsController>(nameof(SensorsController.ChangeStatus));
        AssertProtected<SensorsController>(nameof(SensorsController.Update));
        AssertProtected<AlertRulesController>(nameof(AlertRulesController.Create));
        AssertProtected<AlertRulesController>(nameof(AlertRulesController.ChangeStatus));
        AssertProtected<AlertsController>(nameof(AlertsController.Acknowledge));
        AssertProtected<AlertsController>(nameof(AlertsController.Resolve));
        AssertProtected<SensorReadingsController>(nameof(SensorReadingsController.Create));
    }

    [Fact]
    public void CommunityCrudEndpointsAreExposed()
    {
        Assert.Equal("api/communities", RouteOf<CommunitiesController>());
        AssertMethod<CommunitiesController>(nameof(CommunitiesController.GetAll), typeof(HttpGetAttribute), null);
        AssertMethod<CommunitiesController>(nameof(CommunitiesController.GetById), typeof(HttpGetAttribute), "{id:guid}");
        AssertMethod<CommunitiesController>(nameof(CommunitiesController.Create), typeof(HttpPostAttribute), null);
        AssertMethod<CommunitiesController>(nameof(CommunitiesController.Update), typeof(HttpPutAttribute), "{id:guid}");
        AssertMethod<CommunitiesController>(nameof(CommunitiesController.Delete), typeof(HttpDeleteAttribute), "{id:guid}");
    }
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
        AssertMethod<AlertsController>(nameof(AlertsController.Acknowledge), typeof(HttpPatchAttribute), "{id:guid}/acknowledge");
        AssertMethod<AlertsController>(nameof(AlertsController.Resolve), typeof(HttpPatchAttribute), "{id:guid}/resolve");
    }

    [Fact]
    public void AuditLogEndpointIsAdministratorOnly()
    {
        Assert.Equal("api/audit-actions", RouteOf<AuditActionsController>());
        AssertMethod<AuditActionsController>(nameof(AuditActionsController.GetRecent), typeof(HttpGetAttribute), null);
        AuthorizeAttribute attribute = Assert.Single(typeof(AuditActionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
        Assert.Equal("Administrator", attribute.Roles);
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

    private static void AssertProtected<TController>(string name)
    {
        AuthorizeAttribute attribute = Assert.Single(typeof(TController).GetMethod(name)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
        Assert.Equal("Administrator", attribute.Roles);
    }
}
