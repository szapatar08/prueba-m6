using FluentAssertions;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Handlers;

namespace Prueba.UnitTests.Modules.Notifications.Handlers;

public class KycCompletedEventHandlerTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly KycCompletedEventHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public KycCompletedEventHandlerTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _emailServiceMock = new Mock<IEmailService>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _handler = new KycCompletedEventHandler(
            _repositoryMock.Object,
            _currentTenantMock.Object,
            _emailServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenApproved_ShouldCreateNotificationWithApprovedTitle()
    {
        // Arrange
        var userId = Guid.NewGuid();

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
    public async Task HandleAsync_WhenApproved_ShouldSendApprovedEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _handler.HandleAsync(userId, "Approved");

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "user@example.com",
            "KYC Verification Approved",
            It.Is<string>(s => s.Contains("approved")),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenRejected_ShouldSendRejectedEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _handler.HandleAsync(userId, "Rejected");

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "user@example.com",
            "KYC Verification Rejected",
            It.Is<string>(s => s.Contains("rejected")),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldSaveBeforeSendingEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _handler.HandleAsync(userId, "Approved");

        // Assert
        _repositoryMock.Verify(r => r.SaveChangesAsync(CancellationToken.None), Times.Once);
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once);
    }
}
