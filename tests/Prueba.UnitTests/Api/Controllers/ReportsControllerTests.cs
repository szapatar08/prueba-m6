using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Prueba.Api.Controllers;
using Prueba.Application.Interfaces;

namespace Prueba.UnitTests.Api.Controllers;

public class ReportsControllerTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly ReportsController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public ReportsControllerTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _controller = new ReportsController(_repositoryMock.Object, _currentTenantMock.Object);

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
        var controllerType = typeof(ReportsController);

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
        var controllerType = typeof(ReportsController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(ApiControllerAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
    }

    [Fact]
    public void Controller_ShouldHaveCorrectRoute()
    {
        // Arrange
        var controllerType = typeof(ReportsController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(RouteAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
        ((RouteAttribute)attributes[0]).Template.Should().Be("api/[controller]");
    }

    [Fact]
    public void GetPropertyReport_ShouldExist()
    {
        // Verify GetPropertyReport method exists
        var method = typeof(ReportsController).GetMethod(nameof(ReportsController.GetPropertyReport));
        method.Should().NotBeNull("GetPropertyReport method must exist");
    }

    [Fact]
    public void GetPortfolioReport_ShouldExist()
    {
        // Verify GetPortfolioReport method exists
        var method = typeof(ReportsController).GetMethod(nameof(ReportsController.GetPortfolioReport));
        method.Should().NotBeNull("GetPortfolioReport method must exist");
    }

    [Fact]
    public void GetPropertyReport_ShouldAcceptParameters()
    {
        // Verify GetPropertyReport accepts propertyId, startDate, endDate
        var controllerType = typeof(ReportsController);
        var method = controllerType.GetMethod(nameof(ReportsController.GetPropertyReport));

        method.Should().NotBeNull("GetPropertyReport method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(4, "GetPropertyReport should accept propertyId, startDate, endDate, and cancellationToken");
        parameters[0].ParameterType.Should().Be(typeof(Guid), "First parameter should be Guid propertyId");
        parameters[1].ParameterType.Should().Be(typeof(DateOnly), "Second parameter should be DateOnly startDate");
        parameters[2].ParameterType.Should().Be(typeof(DateOnly), "Third parameter should be DateOnly endDate");
        parameters[3].ParameterType.Should().Be(typeof(CancellationToken), "Fourth parameter should be CancellationToken");
    }

    [Fact]
    public void GetPortfolioReport_ShouldAcceptParameters()
    {
        // Verify GetPortfolioReport accepts startDate, endDate
        var controllerType = typeof(ReportsController);
        var method = controllerType.GetMethod(nameof(ReportsController.GetPortfolioReport));

        method.Should().NotBeNull("GetPortfolioReport method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3, "GetPortfolioReport should accept startDate, endDate, and cancellationToken");
        parameters[0].ParameterType.Should().Be(typeof(DateOnly), "First parameter should be DateOnly startDate");
        parameters[1].ParameterType.Should().Be(typeof(DateOnly), "Second parameter should be DateOnly endDate");
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken), "Third parameter should be CancellationToken");
    }
}
