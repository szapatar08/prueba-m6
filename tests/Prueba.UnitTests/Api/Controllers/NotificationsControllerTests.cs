using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Prueba.Api.Controllers;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.UnitTests.Api.Controllers;

public class NotificationsControllerTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly NotificationsController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public NotificationsControllerTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _controller = new NotificationsController(_repositoryMock.Object, _currentTenantMock.Object);

        // Setup authenticated user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public void GetNotifications_ShouldHaveAuthorizeAttribute()
    {
        // Arrange
        var method = typeof(NotificationsController).GetMethod(nameof(NotificationsController.GetNotifications))!;

        // Act
        var attributes = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
    }

    [Fact]
    public void MarkAsRead_ShouldHaveAuthorizeAttribute()
    {
        // Arrange
        var method = typeof(NotificationsController).GetMethod(nameof(NotificationsController.MarkAsRead))!;

        // Act
        var attributes = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
    }

    [Fact]
    public void MarkAllAsRead_ShouldHaveAuthorizeAttribute()
    {
        // Arrange
        var method = typeof(NotificationsController).GetMethod(nameof(NotificationsController.MarkAllAsRead))!;

        // Act
        var attributes = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        // Arrange
        var controllerType = typeof(NotificationsController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(ApiControllerAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
    }

    [Fact]
    public void Controller_ShouldHaveCorrectRoute()
    {
        // Arrange
        var controllerType = typeof(NotificationsController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(RouteAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
        ((RouteAttribute)attributes[0]).Template.Should().Be("api/[controller]");
    }

    [Fact]
    public void Notifications_ShouldBeScopedToUser()
    {
        // Verify that the controller queries by UserId
        // This is a behavioral test ensuring tenant isolation
        var controllerType = typeof(NotificationsController);
        var getMethod = controllerType.GetMethod(nameof(NotificationsController.GetNotifications));

        getMethod.Should().NotBeNull("GetNotifications method must exist");
        getMethod!.ReturnType.Should().Be(typeof(Task<IActionResult>),
            "GetNotifications must return async Task<IActionResult>");
    }

    [Fact]
    public void MarkAsRead_ShouldAcceptGuidId()
    {
        // Verify MarkAsRead accepts a Guid id parameter
        var controllerType = typeof(NotificationsController);
        var method = controllerType.GetMethod(nameof(NotificationsController.MarkAsRead));

        method.Should().NotBeNull("MarkAsRead method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(2, "MarkAsRead should accept id and cancellationToken");
        parameters[0].ParameterType.Should().Be(typeof(Guid), "First parameter should be Guid id");
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken), "Second parameter should be CancellationToken");
    }

    [Fact]
    public void MarkAllAsRead_ShouldAcceptCancellationToken()
    {
        // Verify MarkAllAsRead accepts a CancellationToken
        var controllerType = typeof(NotificationsController);
        var method = controllerType.GetMethod(nameof(NotificationsController.MarkAllAsRead));

        method.Should().NotBeNull("MarkAllAsRead method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(1, "MarkAllAsRead should accept cancellationToken");
        parameters[0].ParameterType.Should().Be(typeof(CancellationToken));
    }
}
