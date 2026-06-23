using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Prueba.Api.Middleware;
using Prueba.Application.Interfaces;

namespace Prueba.UnitTests.Middleware;

public class TenantResolutionTests
{
    [Fact]
    public async Task InvokeAsync_WithTenantClaim_ShouldSetTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantMock = new Mock<ICurrentTenant>();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", tenantId.ToString())
        }));

        var wasCalled = false;
        RequestDelegate next = ctx => { wasCalled = true; return Task.CompletedTask; };

        var middleware = new TenantResolution(next);

        // Act
        await middleware.InvokeAsync(httpContext, tenantMock.Object);

        // Assert
        tenantMock.Verify(t => t.SetTenant(tenantId), Times.Once);
        wasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithoutTenantClaim_ShouldNotSetTenant()
    {
        // Arrange
        var tenantMock = new Mock<ICurrentTenant>();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var wasCalled = false;
        RequestDelegate next = ctx => { wasCalled = true; return Task.CompletedTask; };

        var middleware = new TenantResolution(next);

        // Act
        await middleware.InvokeAsync(httpContext, tenantMock.Object);

        // Assert
        tenantMock.Verify(t => t.SetTenant(It.IsAny<Guid>()), Times.Never);
        wasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidTenantClaim_ShouldNotSetTenant()
    {
        // Arrange
        var tenantMock = new Mock<ICurrentTenant>();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", "not-a-guid")
        }));

        var wasCalled = false;
        RequestDelegate next = ctx => { wasCalled = true; return Task.CompletedTask; };

        var middleware = new TenantResolution(next);

        // Act
        await middleware.InvokeAsync(httpContext, tenantMock.Object);

        // Assert
        tenantMock.Verify(t => t.SetTenant(It.IsAny<Guid>()), Times.Never);
        wasCalled.Should().BeTrue();
    }
}
