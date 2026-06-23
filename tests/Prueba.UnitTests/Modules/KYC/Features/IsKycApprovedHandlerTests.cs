using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.KYC.Entities;
using Prueba.Modules.KYC.Features.IsKycApproved;

namespace Prueba.UnitTests.Modules.KYC.Features;

public class IsKycApprovedHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly IsKycApprovedHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public IsKycApprovedHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _currentTenantMock = new Mock<ICurrentTenant>();
        _currentTenantMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentTenantMock.Setup(x => x.SchemaName).Returns("public");

        _context = new AppDbContext(options, _currentTenantMock.Object);
        _context.Database.EnsureCreated();
        _repository = new Repository(_context);
        _handler = new IsKycApprovedHandler(_repository, _currentTenantMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithNoValidation_ShouldReturnFalse()
    {
        // Act
        var result = await _handler.HandleAsync(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithPendingValidation_ShouldReturnFalse()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithApprovedValidation_ShouldReturnTrue()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        validation.Approve("John Doe", "DOC-12345678", new DateTime(1990, 1, 15));
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithRejectedValidation_ShouldReturnFalse()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        validation.Reject("Document unreadable");
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
