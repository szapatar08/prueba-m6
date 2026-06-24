using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.KYC.Entities;
using Prueba.Modules.KYC.Jobs;

namespace Prueba.UnitTests.Modules.KYC.Jobs;

public class ProcessKycDocumentJobTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<IOcrService> _mockOcrService;
    private readonly Mock<IObjectStorage> _mockObjectStorage;
    private readonly Mock<ILogger<ProcessKycDocumentJob>> _mockLogger;
    private readonly ProcessKycDocumentJob _job;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ProcessKycDocumentJobTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        var currentTenantMock = new Mock<ICurrentTenant>();
        currentTenantMock.Setup(x => x.TenantId).Returns(_tenantId);
        currentTenantMock.Setup(x => x.SchemaName).Returns("public");

        _context = new AppDbContext(options, currentTenantMock.Object);
        _context.Database.EnsureCreated();
        _repository = new Repository(_context);

        _mockOcrService = new Mock<IOcrService>();
        _mockObjectStorage = new Mock<IObjectStorage>();
        _mockLogger = new Mock<ILogger<ProcessKycDocumentJob>>();

        _job = new ProcessKycDocumentJob(
            _repository,
            _mockOcrService.Object,
            _mockObjectStorage.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithHighConfidence_ShouldApprove()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "passport", _tenantId);
        var document = KycDocument.Create(validation.Id, "passport.jpg", "image/jpeg", "path/to/file.jpg", _tenantId);
        _repository.Add(validation);
        _repository.Add(document);
        await _repository.SaveChangesAsync();

        _mockObjectStorage
            .Setup(s => s.DownloadAsync("kyc-documents", document.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        _mockOcrService
            .Setup(s => s.ExtractDocumentDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult("John", "Doe", "DOC123", new DateTime(1990, 1, 15), 0.95, null));

        // Act
        await _job.ExecuteAsync(validation.Id, CancellationToken.None);

        // Assert
        var updated = await _repository.Query<KycValidation>().IgnoreQueryFilters()
            .FirstAsync(v => v.Id == validation.Id);
        updated.Status.Should().Be(KycStatus.Approved);
        updated.ConfidenceScore.Should().Be(95.0);
        updated.ExtractedNames.Should().Be("John Doe");
    }

    [Fact]
    public async Task ExecuteAsync_WithLowConfidence_ShouldReject()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "passport", _tenantId);
        var document = KycDocument.Create(validation.Id, "passport.jpg", "image/jpeg", "path/to/file.jpg", _tenantId);
        _repository.Add(validation);
        _repository.Add(document);
        await _repository.SaveChangesAsync();

        _mockObjectStorage
            .Setup(s => s.DownloadAsync("kyc-documents", document.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        _mockOcrService
            .Setup(s => s.ExtractDocumentDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult("John", "Doe", "DOC123", new DateTime(1990, 1, 15), 0.50, null));

        // Act
        await _job.ExecuteAsync(validation.Id, CancellationToken.None);

        // Assert
        var updated = await _repository.Query<KycValidation>().IgnoreQueryFilters()
            .FirstAsync(v => v.Id == validation.Id);
        updated.Status.Should().Be(KycStatus.Rejected);
        updated.ExtractionErrors.Should().Contain("Low confidence");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyProcessed_ShouldSkip()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "passport", _tenantId);
        validation.Approve("Test", "DOC", new DateTime(1990, 1, 1), 95.0);
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        await _job.ExecuteAsync(validation.Id, CancellationToken.None);

        // Assert — OCR service should not be called
        _mockOcrService.Verify(
            s => s.ExtractDocumentDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationNotFound_ShouldReturnGracefully()
    {
        // Act — should not throw
        await _job.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert — OCR service should not be called
        _mockOcrService.Verify(
            s => s.ExtractDocumentDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentNotFound_ShouldReject()
    {
        // Arrange — validation exists but no document
        var validation = KycValidation.Create(_userId, "passport", _tenantId);
        _repository.Add(validation);
        await _repository.SaveChangesAsync();

        // Act
        await _job.ExecuteAsync(validation.Id, CancellationToken.None);

        // Assert
        var updated = await _repository.Query<KycValidation>().IgnoreQueryFilters()
            .FirstAsync(v => v.Id == validation.Id);
        updated.Status.Should().Be(KycStatus.Rejected);
        updated.ExtractionErrors.Should().Contain("No document found");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOcrThrowsTransientError_ShouldPropagateForRetry()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "passport", _tenantId);
        var document = KycDocument.Create(validation.Id, "passport.jpg", "image/jpeg", "path/to/file.jpg", _tenantId);
        _repository.Add(validation);
        _repository.Add(document);
        await _repository.SaveChangesAsync();

        _mockObjectStorage
            .Setup(s => s.DownloadAsync("kyc-documents", document.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        _mockOcrService
            .Setup(s => s.ExtractDocumentDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OcrException("API unavailable"));

        // Act
        var act = () => _job.ExecuteAsync(validation.Id, CancellationToken.None);

        // Assert — should throw to allow Hangfire retry
        await act.Should().ThrowAsync<OcrException>();

        // Validation should remain Pending (not processed yet)
        var updated = await _repository.Query<KycValidation>().IgnoreQueryFilters()
            .FirstAsync(v => v.Id == validation.Id);
        updated.Status.Should().Be(KycStatus.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_WithExactThreshold_ShouldApprove()
    {
        // Arrange — confidence exactly at 80% threshold
        var validation = KycValidation.Create(_userId, "passport", _tenantId);
        var document = KycDocument.Create(validation.Id, "passport.jpg", "image/jpeg", "path/to/file.jpg", _tenantId);
        _repository.Add(validation);
        _repository.Add(document);
        await _repository.SaveChangesAsync();

        _mockObjectStorage
            .Setup(s => s.DownloadAsync("kyc-documents", document.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        _mockOcrService
            .Setup(s => s.ExtractDocumentDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult("John", "Doe", "DOC123", new DateTime(1990, 1, 15), 0.80, null));

        // Act
        await _job.ExecuteAsync(validation.Id, CancellationToken.None);

        // Assert
        var updated = await _repository.Query<KycValidation>().IgnoreQueryFilters()
            .FirstAsync(v => v.Id == validation.Id);
        updated.Status.Should().Be(KycStatus.Approved);
    }

    [Fact]
    public async Task ExecuteAsync_JustBelowThreshold_ShouldReject()
    {
        // Arrange — confidence just below 80% threshold
        var validation = KycValidation.Create(_userId, "passport", _tenantId);
        var document = KycDocument.Create(validation.Id, "passport.jpg", "image/jpeg", "path/to/file.jpg", _tenantId);
        _repository.Add(validation);
        _repository.Add(document);
        await _repository.SaveChangesAsync();

        _mockObjectStorage
            .Setup(s => s.DownloadAsync("kyc-documents", document.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        _mockOcrService
            .Setup(s => s.ExtractDocumentDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult("John", "Doe", "DOC123", new DateTime(1990, 1, 15), 0.79, null));

        // Act
        await _job.ExecuteAsync(validation.Id, CancellationToken.None);

        // Assert
        var updated = await _repository.Query<KycValidation>().IgnoreQueryFilters()
            .FirstAsync(v => v.Id == validation.Id);
        updated.Status.Should().Be(KycStatus.Rejected);
    }

    [Fact]
    public async Task ExecuteAsync_WithOcrErrorMessage_ShouldIncludeInRejection()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, "passport", _tenantId);
        var document = KycDocument.Create(validation.Id, "passport.jpg", "image/jpeg", "path/to/file.jpg", _tenantId);
        _repository.Add(validation);
        _repository.Add(document);
        await _repository.SaveChangesAsync();

        _mockObjectStorage
            .Setup(s => s.DownloadAsync("kyc-documents", document.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        _mockOcrService
            .Setup(s => s.ExtractDocumentDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult("", "", "", null, 0.30, "Partial text detected"));

        // Act
        await _job.ExecuteAsync(validation.Id, CancellationToken.None);

        // Assert
        var updated = await _repository.Query<KycValidation>().IgnoreQueryFilters()
            .FirstAsync(v => v.Id == validation.Id);
        updated.Status.Should().Be(KycStatus.Rejected);
        updated.ExtractionErrors.Should().Contain("Low confidence");
        updated.ExtractionErrors.Should().Contain("Partial text detected");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
