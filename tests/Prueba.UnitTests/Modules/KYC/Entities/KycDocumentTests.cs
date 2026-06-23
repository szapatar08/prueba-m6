using FluentAssertions;
using Prueba.Modules.KYC.Entities;

namespace Prueba.UnitTests.Modules.KYC.Entities;

public class KycDocumentTests
{
    private readonly Guid _kycValidationId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string FileName = "passport.jpg";
    private const string ContentType = "image/jpeg";
    private const string StoragePath = "tenant/user/guid_passport.jpg";

    [Fact]
    public void Create_WithValidParameters_ShouldCreateDocument()
    {
        // Act
        var document = KycDocument.Create(_kycValidationId, FileName, ContentType, StoragePath, _tenantId);

        // Assert
        document.Should().NotBeNull();
        document.Id.Should().NotBeEmpty();
        document.KycValidationId.Should().Be(_kycValidationId);
        document.FileName.Should().Be(FileName);
        document.ContentType.Should().Be(ContentType);
        document.StoragePath.Should().Be(StoragePath);
        document.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        document.TenantId.Should().Be(_tenantId);
        document.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        // Act
        var document1 = KycDocument.Create(_kycValidationId, FileName, ContentType, StoragePath, _tenantId);
        var document2 = KycDocument.Create(Guid.NewGuid(), FileName, ContentType, StoragePath, _tenantId);

        // Assert
        document1.Id.Should().NotBe(document2.Id);
    }

    [Fact]
    public void Create_WithEmptyKycValidationId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => KycDocument.Create(Guid.Empty, FileName, ContentType, StoragePath, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*KYC Validation ID*");
    }

    [Fact]
    public void Create_WithEmptyFileName_ShouldThrowArgumentException()
    {
        // Act
        var act = () => KycDocument.Create(_kycValidationId, "", ContentType, StoragePath, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*File name*");
    }

    [Fact]
    public void Create_WithEmptyContentType_ShouldThrowArgumentException()
    {
        // Act
        var act = () => KycDocument.Create(_kycValidationId, FileName, "", StoragePath, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content type*");
    }

    [Fact]
    public void Create_WithEmptyStoragePath_ShouldThrowArgumentException()
    {
        // Act
        var act = () => KycDocument.Create(_kycValidationId, FileName, ContentType, "", _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Storage path*");
    }

    [Fact]
    public void Create_ShouldSetUploadedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var document = KycDocument.Create(_kycValidationId, FileName, ContentType, StoragePath, _tenantId);

        // Assert
        var after = DateTime.UtcNow;
        document.UploadedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
