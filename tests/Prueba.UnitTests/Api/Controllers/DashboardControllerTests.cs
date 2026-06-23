using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Prueba.Api.Controllers;
using Prueba.Application.Interfaces;

namespace Prueba.UnitTests.Api.Controllers;

public class DashboardControllerTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly DashboardController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public DashboardControllerTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _controller = new DashboardController(_repositoryMock.Object, _currentTenantMock.Object);

        // Setup authenticated user with Owner role
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString()),
            new(ClaimTypes.Role, "Owner")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        // Arrange
        var controllerType = typeof(DashboardController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
        ((AuthorizeAttribute)attributes[0]).Roles.Should().Contain("Owner");
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        // Arrange
        var controllerType = typeof(DashboardController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(ApiControllerAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
    }

    [Fact]
    public void Controller_ShouldHaveCorrectRoute()
    {
        // Arrange
        var controllerType = typeof(DashboardController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(RouteAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
        ((RouteAttribute)attributes[0]).Template.Should().Be("api/[controller]");
    }

    [Fact]
    public void GetOccupancyRate_ShouldExist()
    {
        // Verify GetOccupancyRate method exists
        var method = typeof(DashboardController).GetMethod(nameof(DashboardController.GetOccupancyRate));
        method.Should().NotBeNull("GetOccupancyRate method must exist");
    }

    [Fact]
    public void GetRevenue_ShouldExist()
    {
        // Verify GetRevenue method exists
        var method = typeof(DashboardController).GetMethod(nameof(DashboardController.GetRevenue));
        method.Should().NotBeNull("GetRevenue method must exist");
    }

    [Fact]
    public void GetBookingTrends_ShouldExist()
    {
        // Verify GetBookingTrends method exists
        var method = typeof(DashboardController).GetMethod(nameof(DashboardController.GetBookingTrends));
        method.Should().NotBeNull("GetBookingTrends method must exist");
    }

    [Fact]
    public void GetOccupancyRate_ShouldAcceptQueryParameters()
    {
        // Verify GetOccupancyRate accepts propertyId, startDate, endDate
        var controllerType = typeof(DashboardController);
        var method = controllerType.GetMethod(nameof(DashboardController.GetOccupancyRate));

        method.Should().NotBeNull("GetOccupancyRate method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(4, "GetOccupancyRate should accept propertyId, startDate, endDate, and cancellationToken");
        parameters[0].ParameterType.Should().Be(typeof(Guid), "First parameter should be Guid propertyId");
        parameters[1].ParameterType.Should().Be(typeof(DateOnly), "Second parameter should be DateOnly startDate");
        parameters[2].ParameterType.Should().Be(typeof(DateOnly), "Third parameter should be DateOnly endDate");
        parameters[3].ParameterType.Should().Be(typeof(CancellationToken), "Fourth parameter should be CancellationToken");
    }

    [Fact]
    public void GetRevenue_ShouldAcceptQueryParameters()
    {
        // Verify GetRevenue accepts propertyId, startDate, endDate
        var controllerType = typeof(DashboardController);
        var method = controllerType.GetMethod(nameof(DashboardController.GetRevenue));

        method.Should().NotBeNull("GetRevenue method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(4, "GetRevenue should accept propertyId, startDate, endDate, and cancellationToken");
        parameters[0].ParameterType.Should().Be(typeof(Guid), "First parameter should be Guid propertyId");
        parameters[1].ParameterType.Should().Be(typeof(DateOnly), "Second parameter should be DateOnly startDate");
        parameters[2].ParameterType.Should().Be(typeof(DateOnly), "Third parameter should be DateOnly endDate");
        parameters[3].ParameterType.Should().Be(typeof(CancellationToken), "Fourth parameter should be CancellationToken");
    }

    [Fact]
    public void GetBookingTrends_ShouldAcceptQueryParameters()
    {
        // Verify GetBookingTrends accepts propertyId, period
        var controllerType = typeof(DashboardController);
        var method = controllerType.GetMethod(nameof(DashboardController.GetBookingTrends));

        method.Should().NotBeNull("GetBookingTrends method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3, "GetBookingTrends should accept propertyId, period, and cancellationToken");
        parameters[0].ParameterType.Should().Be(typeof(Guid), "First parameter should be Guid propertyId");
        parameters[1].ParameterType.Should().Be(typeof(string), "Second parameter should be string period");
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken), "Third parameter should be CancellationToken");
    }
}
