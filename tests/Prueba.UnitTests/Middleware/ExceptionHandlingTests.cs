using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Prueba.Api.Middleware;

namespace Prueba.UnitTests.Middleware;

public class ExceptionHandlingTests
{
    [Fact]
    public async Task InvokeAsync_NoException_ShouldCallNext()
    {
        // Arrange
        var wasCalled = false;
        RequestDelegate next = ctx => { wasCalled = true; return Task.CompletedTask; };
        var loggerMock = new Mock<ILogger<ExceptionHandling>>();
        var middleware = new ExceptionHandling(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        wasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_ShouldReturn400WithGenericMessage()
    {
        // Arrange
        RequestDelegate next = _ => throw new ArgumentException("internal detail");
        var loggerMock = new Mock<ILogger<ExceptionHandling>>();
        var middleware = new ExceptionHandling(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        body.Should().Contain("Invalid request parameters");
        body.Should().NotContain("internal detail");
    }

    [Fact]
    public async Task InvokeAsync_UnknownException_ShouldReturn500()
    {
        // Arrange
        RequestDelegate next = _ => throw new Exception("unexpected");
        var loggerMock = new Mock<ILogger<ExceptionHandling>>();
        var middleware = new ExceptionHandling(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_ShouldReturn401()
    {
        // Arrange
        RequestDelegate next = _ => throw new UnauthorizedAccessException();
        var loggerMock = new Mock<ILogger<ExceptionHandling>>();
        var middleware = new ExceptionHandling(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_ShouldReturn404()
    {
        // Arrange
        RequestDelegate next = _ => throw new KeyNotFoundException();
        var loggerMock = new Mock<ILogger<ExceptionHandling>>();
        var middleware = new ExceptionHandling(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }
}
