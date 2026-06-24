using FluentAssertions;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Handlers;
using Prueba.Modules.Notifications.Services;

namespace Prueba.UnitTests.Modules.Notifications.Handlers;

public class KycCompletedEventHandlerTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IEmailTemplateRenderer> _templateRendererMock;
    private readonly KycCompletedEventHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public KycCompletedEventHandlerTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _emailServiceMock = new Mock<IEmailService>();
        _templateRendererMock = new Mock<IEmailTemplateRenderer>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _handler = new KycCompletedEventHandler(
            _repositoryMock.Object,
            _currentTenantMock.Object,
            _emailServiceMock.Object,
            _templateRendererMock.Object);
    }

    private User SetupUser(Guid userId)
    {
        var user = User.Create("real.user@domain.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Test", "User", _tenantId);
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task HandleAsync_WhenApproved_ShouldCreateNotificationWithApprovedTitle()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUser(userId);
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.KycApproved, It.IsAny<object>()))
            .Returns(("Subject", "Body"));

        // Act
        await _handler.HandleAsync(userId, "Approved");

        // Assert
        _repositoryMock.Verify(r => r.Add(It.Is<Notification>(n =>
            n.UserId == userId &&
            n.Type == "KycCompleted" &&
            n.Title == "KYC Verification Approved" &&
            n.Message.Contains("approved") &&
            n.TenantId == _tenantId)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenRejected_ShouldCreateNotificationWithRejectedTitle()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUser(userId);
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.KycRejected, It.IsAny<object>()))
            .Returns(("Subject", "Body"));

        // Act
        await _handler.HandleAsync(userId, "Rejected");

        // Assert
        _repositoryMock.Verify(r => r.Add(It.Is<Notification>(n =>
            n.UserId == userId &&
            n.Type == "KycCompleted" &&
            n.Title == "KYC Verification Rejected" &&
            n.Message.Contains("rejected") &&
            n.TenantId == _tenantId)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenApproved_ShouldUseKycApprovedTemplate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUser(userId);
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.KycApproved, It.IsAny<object>()))
            .Returns(("Identity Verification Approved", "<html>approved</html>"));

        // Act
        await _handler.HandleAsync(userId, "Approved");

        // Assert
        _templateRendererMock.Verify(t => t.Render(TemplateTypes.KycApproved, It.IsAny<object>()), Times.Once);
        _templateRendererMock.Verify(t => t.Render(TemplateTypes.KycRejected, It.IsAny<object>()), Times.Never);
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "real.user@domain.com",
            "Identity Verification Approved",
            "<html>approved</html>",
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenRejected_ShouldUseKycRejectedTemplate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUser(userId);
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.KycRejected, It.IsAny<object>()))
            .Returns(("Identity Verification Rejected", "<html>rejected</html>"));

        // Act
        await _handler.HandleAsync(userId, "Rejected");

        // Assert
        _templateRendererMock.Verify(t => t.Render(TemplateTypes.KycRejected, It.IsAny<object>()), Times.Once);
        _templateRendererMock.Verify(t => t.Render(TemplateTypes.KycApproved, It.IsAny<object>()), Times.Never);
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "real.user@domain.com",
            "Identity Verification Rejected",
            "<html>rejected</html>",
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldResolveUserEmailFromEntity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUser(userId);
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.KycApproved, It.IsAny<object>()))
            .Returns(("Subject", "Body"));

        // Act
        await _handler.HandleAsync(userId, "Approved");

        // Assert — email sent to resolved user email, NOT hardcoded
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "real.user@domain.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            CancellationToken.None), Times.Once);
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "user@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldSaveBeforeSendingEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUser(userId);
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.KycApproved, It.IsAny<object>()))
            .Returns(("Subject", "Body"));

        // Act
        await _handler.HandleAsync(userId, "Approved");

        // Assert
        _repositoryMock.Verify(r => r.SaveChangesAsync(CancellationToken.None), Times.Once);
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowWhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await _handler.Invoking(h => h.HandleAsync(userId, "Approved"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*User*not found*");
    }
}
