using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Jobs;
using Prueba.Modules.Notifications.Services;

namespace Prueba.UnitTests.Modules.Notifications.Jobs;

public class SeedNotificationTemplatesJobTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<ILogger<SeedNotificationTemplatesJob>> _loggerMock;
    private readonly SeedNotificationTemplatesJob _job;
    private readonly Guid _tenantId = Guid.NewGuid();

    public SeedNotificationTemplatesJobTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _loggerMock = new Mock<ILogger<SeedNotificationTemplatesJob>>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _job = new SeedNotificationTemplatesJob(
            _repositoryMock.Object,
            _currentTenantMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSeedAllSevenTemplates()
    {
        // Arrange — no existing templates
        var queryable = new List<NotificationTemplate>().AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert — 7 templates added (BookingCreated + 6 from spec table)
        _repositoryMock.Verify(r => r.Add(It.IsAny<NotificationTemplate>()), Times.Exactly(7));
        _repositoryMock.Verify(r => r.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCreateDuplicates_WhenTemplatesExist()
    {
        // Arrange — all templates already exist
        var existingTemplates = new List<NotificationTemplate>
        {
            NotificationTemplate.Create(TemplateTypes.BookingCreated, "Subject", "Body", _tenantId),
            NotificationTemplate.Create(TemplateTypes.BookingConfirmed, "Subject", "Body", _tenantId),
            NotificationTemplate.Create(TemplateTypes.BookingCancelled, "Subject", "Body", _tenantId),
            NotificationTemplate.Create(TemplateTypes.KycApproved, "Subject", "Body", _tenantId),
            NotificationTemplate.Create(TemplateTypes.KycRejected, "Subject", "Body", _tenantId),
            NotificationTemplate.Create(TemplateTypes.ArrivalReminder, "Subject", "Body", _tenantId),
            NotificationTemplate.Create(TemplateTypes.DepartureReminder, "Subject", "Body", _tenantId),
        };
        var queryable = existingTemplates.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert — no new templates added
        _repositoryMock.Verify(r => r.Add(It.IsAny<NotificationTemplate>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOnlySeedMissingTemplates()
    {
        // Arrange — some templates exist
        var existingTemplates = new List<NotificationTemplate>
        {
            NotificationTemplate.Create(TemplateTypes.BookingCreated, "Subject", "Body", _tenantId),
            NotificationTemplate.Create(TemplateTypes.BookingConfirmed, "Subject", "Body", _tenantId),
        };
        var queryable = existingTemplates.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert — 5 missing templates added (7 total minus 2 existing)
        _repositoryMock.Verify(r => r.Add(It.IsAny<NotificationTemplate>()), Times.Exactly(5));
        _repositoryMock.Verify(r => r.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSeedTemplatesWithValidHandlebarsSyntax()
    {
        // Arrange — no existing templates
        var queryable = new List<NotificationTemplate>().AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);
        var addedTemplates = new List<NotificationTemplate>();
        _repositoryMock.Setup(r => r.Add(It.IsAny<NotificationTemplate>()))
            .Callback<NotificationTemplate>(t => addedTemplates.Add(t));

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert — all templates have Handlebars placeholders
        addedTemplates.Should().HaveCount(7);
        foreach (var template in addedTemplates)
        {
            template.BodyTemplate.Should().NotBeNullOrWhiteSpace();
            template.SubjectTemplate.Should().NotBeNullOrWhiteSpace();
            template.Type.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeAllRequiredTemplateTypes()
    {
        // Arrange — no existing templates
        var queryable = new List<NotificationTemplate>().AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);
        var addedTemplates = new List<NotificationTemplate>();
        _repositoryMock.Setup(r => r.Add(It.IsAny<NotificationTemplate>()))
            .Callback<NotificationTemplate>(t => addedTemplates.Add(t));

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert — all expected types present
        addedTemplates.Should().HaveCount(7);
        var types = addedTemplates.Select(t => t.Type).ToList();
        types.Should().Contain(TemplateTypes.BookingCreated);
        types.Should().Contain(TemplateTypes.BookingConfirmed);
        types.Should().Contain(TemplateTypes.BookingCancelled);
        types.Should().Contain(TemplateTypes.KycApproved);
        types.Should().Contain(TemplateTypes.KycRejected);
        types.Should().Contain(TemplateTypes.ArrivalReminder);
        types.Should().Contain(TemplateTypes.DepartureReminder);
    }
}
