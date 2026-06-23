using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.KYC.Entities;
using Prueba.Modules.KYC.Features.GetKycStatus;

namespace Prueba.UnitTests.Modules.KYC.Features;

public class GetKycStatusHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly GetKycStatusHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetKycStatusHandlerTests()
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
        _handler = new GetKycStatusHandler(_repository, _currentTenantMock.Object);
    }

    [Fact]
    public async Task Handle_WithNoValidation_ShouldReturnPendingStatus()
    {
        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(KycStatus.Pending);
        result.Value.ValidationId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithPendingValidation_ShouldReturnPendingStatus()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(KycStatus.Pending);
        result.Value.ValidationId.Should().Be(validation.Id);
    }

    [Fact]
    public async Task Handle_WithApprovedValidation_ShouldReturnApprovedStatus()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        validation.Approve("John Doe", "DOC-12345678", new DateTime(1990, 1, 15));
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(KycStatus.Approved);
        result.Value.ValidationId.Should().Be(validation.Id);
        result.Value.ExtractedNames.Should().Be("John Doe");
        result.Value.ExtractedDocumentNumber.Should().Be("DOC-12345678");
    }

    [Fact]
    public async Task Handle_WithRejectedValidation_ShouldReturnRejectedStatus()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        validation.Reject("Document unreadable");
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(KycStatus.Rejected);
        result.Value.ValidationId.Should().Be(validation.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnMostRecentValidation()
    {
        // Arrange — create two validations (e.g., user re-uploaded after rejection)
        var validation1 = KycValidation.Create(_userId, "image/jpeg", _tenantId);
        validation1.Reject("Document unreadable");
        _repository.Add(validation1);
        await _repository.SaveChangesAsync();

        var validation2 = KycValidation.Create(_userId, "image/png", _tenantId);
        _repository.Add(validation2);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ValidationId.Should().Be(validation2.Id);
        result.Value.Status.Should().Be(KycStatus.Pending);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
