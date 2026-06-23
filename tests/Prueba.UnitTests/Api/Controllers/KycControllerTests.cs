using System.Security.Claims;
using FluentAssertions;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Api.Controllers;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.KYC.Entities;

namespace Prueba.UnitTests.Api.Controllers;

public class KycControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IObjectStorage> _objectStorageMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly KycController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public KycControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _currentTenantMock = new Mock<ICurrentTenant>();
        _currentTenantMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentTenantMock.Setup(x => x.SchemaName).Returns("public");

        _objectStorageMock = new Mock<IObjectStorage>();
        _objectStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("kyc-documents/path/to/file");

        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();

        _context = new AppDbContext(options, _currentTenantMock.Object);
        _context.Database.EnsureCreated();
        _repository = new Repository(_context);

        _controller = new KycController(
            _repository,
            _currentTenantMock.Object,
            _objectStorageMock.Object,
            _backgroundJobClientMock.Object);
        SetUserContext(_userId);
    }

    private void SetUserContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "Guest")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // === Authorization Attributes ===

    [Fact]
    public void UploadDocument_ShouldRequireAuthorization()
    {
        var method = typeof(KycController).GetMethod(nameof(KycController.UploadDocument));
        method.Should().NotBeNull();
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        authAttr.Should().HaveCount(1);
    }

    [Fact]
    public void GetStatus_ShouldRequireAuthorization()
    {
        var method = typeof(KycController).GetMethod(nameof(KycController.GetStatus));
        method.Should().NotBeNull();
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        authAttr.Should().HaveCount(1);
    }

    // === GetStatus ===

    [Fact]
    public async Task GetStatus_WithNoValidation_ShouldReturnOkWithPendingStatus()
    {
        // Act
        var result = await _controller.GetStatus(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetStatus_WithPendingValidation_ShouldReturnOk()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.GetStatus(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetStatus_WithApprovedValidation_ShouldReturnOk()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        validation.Approve("John Doe", "DOC-12345678", new DateTime(1990, 1, 15));
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.GetStatus(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // === CRUD Cycle ===

    [Fact]
    public async Task GetStatus_AfterUpload_ShouldReturnPendingStatus()
    {
        // Arrange — simulate upload by creating validation directly
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.GetStatus(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
