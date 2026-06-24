using FluentAssertions;
using Prueba.Modules.KYC.Entities;

namespace Prueba.UnitTests.Modules.KYC.Entities;

public class KycValidationTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string DocumentType = "image/jpeg";

    [Fact]
    public void Create_WithValidParameters_ShouldCreateValidation()
    {
        // Act
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Assert
        validation.Should().NotBeNull();
        validation.Id.Should().NotBeEmpty();
        validation.UserId.Should().Be(_userId);
        validation.Status.Should().Be(KycStatus.Pending);
        validation.DocumentType.Should().Be(DocumentType);
        validation.TenantId.Should().Be(_tenantId);
        validation.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        // Act
        var validation1 = KycValidation.Create(_userId, DocumentType, _tenantId);
        var validation2 = KycValidation.Create(Guid.NewGuid(), DocumentType, _tenantId);

        // Assert
        validation1.Id.Should().NotBe(validation2.Id);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => KycValidation.Create(Guid.Empty, DocumentType, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*User ID*");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => KycValidation.Create(_userId, DocumentType, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tenant ID*");
    }

    [Fact]
    public void Create_WithEmptyDocumentType_ShouldThrowArgumentException()
    {
        // Act
        var act = () => KycValidation.Create(_userId, "", _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Document type*");
    }

    [Fact]
    public void Create_ShouldSetCreatedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Assert
        var after = DateTime.UtcNow;
        validation.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Approve_FromPending_ShouldSetStatusToApproved()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Act
        validation.Approve("John Doe", "DOC-12345678", new DateTime(1990, 1, 15), 95.0);

        // Assert
        validation.Status.Should().Be(KycStatus.Approved);
        validation.ExtractedNames.Should().Be("John Doe");
        validation.ExtractedDocumentNumber.Should().Be("DOC-12345678");
        validation.ExtractedDateOfBirth.Should().Be(new DateTime(1990, 1, 15));
        validation.ProcessedAt.Should().NotBeNull();
        validation.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Approve_FromPending_ShouldRaiseDomainEvent()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Act
        validation.Approve("John Doe", "DOC-12345678", new DateTime(1990, 1, 15), 95.0);

        // Assert
        validation.DomainEvents.Should().HaveCount(1);
        var domainEvent = validation.DomainEvents.First();
        domainEvent.Should().BeOfType<Prueba.Modules.KYC.Events.KycCompleted>();
    }

    [Fact]
    public void Approve_FromNonPending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);
        validation.Approve("John Doe", "DOC-12345678", new DateTime(1990, 1, 15), 95.0);

        // Act
        var act = () => validation.Approve("Jane Doe", "DOC-87654321", new DateTime(1995, 5, 20), 90.0);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot approve*");
    }

    [Fact]
    public void Reject_FromPending_ShouldSetStatusToRejected()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Act
        validation.Reject("Document unreadable");

        // Assert
        validation.Status.Should().Be(KycStatus.Rejected);
        validation.ProcessedAt.Should().NotBeNull();
        validation.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Reject_FromPending_ShouldRaiseDomainEvent()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Act
        validation.Reject("Document unreadable");

        // Assert
        validation.DomainEvents.Should().HaveCount(1);
        var domainEvent = validation.DomainEvents.First();
        domainEvent.Should().BeOfType<Prueba.Modules.KYC.Events.KycCompleted>();
    }

    [Fact]
    public void Reject_FromNonPending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);
        validation.Reject("Document unreadable");

        // Act
        var act = () => validation.Reject("Another reason");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot reject*");
    }

    [Fact]
    public void IsApproved_WhenApproved_ShouldReturnTrue()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);
        validation.Approve("John Doe", "DOC-12345678", new DateTime(1990, 1, 15), 95.0);

        // Act
        var isApproved = validation.IsApproved;

        // Assert
        isApproved.Should().BeTrue();
    }

    [Fact]
    public void IsApproved_WhenPending_ShouldReturnFalse()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Act
        var isApproved = validation.IsApproved;

        // Assert
        isApproved.Should().BeFalse();
    }

    [Fact]
    public void IsApproved_WhenRejected_ShouldReturnFalse()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);
        validation.Reject("Document unreadable");

        // Act
        var isApproved = validation.IsApproved;

        // Assert
        isApproved.Should().BeFalse();
    }

    [Fact]
    public void Approve_ShouldPersistConfidenceScore()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Act
        validation.Approve("John Doe", "DOC-12345678", new DateTime(1990, 1, 15), 89.5);

        // Assert
        validation.ConfidenceScore.Should().Be(89.5);
    }

    [Fact]
    public void Reject_ShouldPersistExtractionErrors()
    {
        // Arrange
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Act
        validation.Reject("Low confidence: 58.75%");

        // Assert
        validation.ExtractionErrors.Should().Be("Low confidence: 58.75%");
    }

    [Fact]
    public void Create_ShouldHaveNullConfidenceScore()
    {
        // Act
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Assert
        validation.ConfidenceScore.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldHaveNullExtractionErrors()
    {
        // Act
        var validation = KycValidation.Create(_userId, DocumentType, _tenantId);

        // Assert
        validation.ExtractionErrors.Should().BeNull();
    }
}
