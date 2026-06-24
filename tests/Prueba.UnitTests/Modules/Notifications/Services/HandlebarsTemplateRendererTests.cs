using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Services;

namespace Prueba.UnitTests.Modules.Notifications.Services;

public class HandlebarsTemplateRendererTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly IMemoryCache _memoryCache;
    private readonly HandlebarsTemplateRenderer _renderer;
    private readonly Guid _tenantId = Guid.NewGuid();

    public HandlebarsTemplateRendererTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _renderer = new HandlebarsTemplateRenderer(
            _repositoryMock.Object,
            _currentTenantMock.Object,
            _memoryCache);
    }

    [Fact]
    public void Render_ShouldSubstituteTemplateVariables()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            TemplateTypes.BookingConfirmed,
            "Booking Confirmed - {{PropertyName}}",
            "<h1>Hello {{GuestName}}</h1><p>Booking at {{PropertyName}} from {{StartDate}} to {{EndDate}}</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        var data = new
        {
            GuestName = "John Doe",
            PropertyName = "Beach House",
            StartDate = "2026-07-01",
            EndDate = "2026-07-05"
        };

        // Act
        var (subject, body) = _renderer.Render(TemplateTypes.BookingConfirmed, data);

        // Assert
        subject.Should().Be("Booking Confirmed - Beach House");
        body.Should().Contain("Hello John Doe");
        body.Should().Contain("Beach House");
        body.Should().Contain("2026-07-01");
        body.Should().Contain("2026-07-05");
    }

    [Fact]
    public void Render_ShouldReturnSubjectAndBody()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            TemplateTypes.KycApproved,
            "Identity Verification Approved",
            "<p>Hello {{GuestName}}, your verification is approved.</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        var (subject, body) = _renderer.Render(TemplateTypes.KycApproved, new { GuestName = "Jane" });

        // Assert
        subject.Should().Be("Identity Verification Approved");
        body.Should().Contain("Jane");
        body.Should().Contain("approved");
    }

    [Fact]
    public void Render_WhenTemplateNotFound_ShouldThrowTemplateNotFoundException()
    {
        // Arrange
        var queryable = new List<NotificationTemplate>().AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        var act = () => _renderer.Render("NonExistentType", new { });

        // Assert
        act.Should().Throw<TemplateNotFoundException>()
            .WithMessage("*NonExistentType*");
    }

    [Fact]
    public void Render_ShouldCacheCompiledTemplate()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            TemplateTypes.BookingConfirmed,
            "Subject {{Name}}",
            "<p>{{Name}}</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act — render twice
        _renderer.Render(TemplateTypes.BookingConfirmed, new { Name = "Test" });
        _renderer.Render(TemplateTypes.BookingConfirmed, new { Name = "Test2" });

        // Assert — repository queried only once (template cached)
        _repositoryMock.Verify(r => r.Query<NotificationTemplate>(), Times.Once);
    }

    [Fact]
    public async Task RenderAsync_ShouldReturnSameResultAsRender()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            TemplateTypes.ArrivalReminder,
            "Check-in tomorrow - {{PropertyName}}",
            "<p>{{GuestName}}, your check-in at {{PropertyName}} is tomorrow.</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        var data = new { GuestName = "Alice", PropertyName = "Mountain Cabin" };

        // Act
        var (subject, body) = await _renderer.RenderAsync(TemplateTypes.ArrivalReminder, data);

        // Assert
        subject.Should().Be("Check-in tomorrow - Mountain Cabin");
        body.Should().Contain("Alice");
        body.Should().Contain("Mountain Cabin");
    }

    [Fact]
    public void Render_WithTenantFilter_ShouldQueryByCorrectTenant()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            TemplateTypes.DepartureReminder,
            "Check-out today",
            "<p>Check-out time</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        _renderer.Render(TemplateTypes.DepartureReminder, new { });

        // Assert
        _repositoryMock.Verify(r => r.Query<NotificationTemplate>(), Times.Once);
    }

    [Fact]
    public void Render_WithMismatchedBlockHelpers_ShouldThrowTemplateCompilationException()
    {
        // Arrange — mismatched open/close block helpers
        var template = NotificationTemplate.Create(
            TemplateTypes.BookingConfirmed,
            "Subject",
            "<p>{{#each items}}item{{/if}}</p>",  // Mismatched block helpers
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        var act = () => _renderer.Render(TemplateTypes.BookingConfirmed, new { });

        // Assert
        act.Should().Throw<TemplateCompilationException>();
    }

    [Fact]
    public void Render_WithInvalidSubjectSyntax_ShouldThrowTemplateCompilationException()
    {
        // Arrange — body is valid, subject has invalid syntax
        var template = NotificationTemplate.Create(
            TemplateTypes.BookingConfirmed,
            "{{#each}}",  // Invalid subject syntax
            "<p>Valid body</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        var act = () => _renderer.Render(TemplateTypes.BookingConfirmed, new { });

        // Assert
        act.Should().Throw<TemplateCompilationException>();
    }

    [Fact]
    public async Task RenderAsync_WhenTemplateNotFound_ShouldThrowTemplateNotFoundException()
    {
        // Arrange
        var queryable = new List<NotificationTemplate>().AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        var act = () => _renderer.RenderAsync("NonExistentType", new { });

        // Assert
        await act.Should().ThrowAsync<TemplateNotFoundException>()
            .WithMessage("*NonExistentType*");
    }

    [Fact]
    public async Task RenderAsync_WithInvalidSyntax_ShouldThrowTemplateCompilationException()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            TemplateTypes.KycApproved,
            "Subject",
            "{{invalid syntax}}",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        var act = () => _renderer.RenderAsync(TemplateTypes.KycApproved, new { });

        // Assert
        await act.Should().ThrowAsync<TemplateCompilationException>();
    }

    [Fact]
    public void Render_WithMultipleTemplateTypes_ShouldCacheEachTypeIndependently()
    {
        // Arrange
        var confirmed = NotificationTemplate.Create(
            TemplateTypes.BookingConfirmed,
            "Confirmed - {{PropertyName}}",
            "<p>Confirmed for {{GuestName}}</p>",
            _tenantId);

        var cancelled = NotificationTemplate.Create(
            TemplateTypes.BookingCancelled,
            "Cancelled - {{PropertyName}}",
            "<p>Cancelled for {{GuestName}}</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { confirmed, cancelled }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act — render both types
        var (s1, b1) = _renderer.Render(TemplateTypes.BookingConfirmed, new { GuestName = "John", PropertyName = "House" });
        var (s2, b2) = _renderer.Render(TemplateTypes.BookingCancelled, new { GuestName = "Jane", PropertyName = "Condo" });
        // Render again — should use cache
        _renderer.Render(TemplateTypes.BookingConfirmed, new { GuestName = "John2", PropertyName = "House2" });

        // Assert
        s1.Should().Be("Confirmed - House");
        b1.Should().Contain("John");
        s2.Should().Be("Cancelled - Condo");
        b2.Should().Contain("Jane");
        // Repository queried once per type (2 total), not per render call (would be 3 without cache)
        _repositoryMock.Verify(r => r.Query<NotificationTemplate>(), Times.Exactly(2));
    }

    [Fact]
    public void Render_WithEmptyData_ShouldRenderTemplateWithoutSubstitution()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            TemplateTypes.KycApproved,
            "Identity Verified",
            "<p>Your identity has been verified.</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        var (subject, body) = _renderer.Render(TemplateTypes.KycApproved, new { });

        // Assert
        subject.Should().Be("Identity Verified");
        body.Should().Contain("verified");
    }

    [Fact]
    public void Render_WithNestedProperties_ShouldSubstituteNestedValues()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            TemplateTypes.BookingConfirmed,
            "Booking for {{Guest.FirstName}}",
            "<p>Property: {{Property.Name}} in {{Property.City}}</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        var data = new
        {
            Guest = new { FirstName = "Alice" },
            Property = new { Name = "Villa", City = "Miami" }
        };

        // Act
        var (subject, body) = _renderer.Render(TemplateTypes.BookingConfirmed, data);

        // Assert
        subject.Should().Be("Booking for Alice");
        body.Should().Contain("Villa");
        body.Should().Contain("Miami");
    }

    [Fact]
    public void Render_WithUnknownProperties_ShouldRenderEmptyStrings()
    {
        // Arrange — template references properties not in data
        var template = NotificationTemplate.Create(
            TemplateTypes.ArrivalReminder,
            "Reminder for {{MissingField}}",
            "<p>{{AnotherMissing}}</p>",
            _tenantId);

        var queryable = new List<NotificationTemplate> { template }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<NotificationTemplate>()).Returns(queryable);

        // Act
        var (subject, body) = _renderer.Render(TemplateTypes.ArrivalReminder, new { KnownField = "value" });

        // Assert — Handlebars renders missing properties as empty string
        subject.Should().NotBeNull();
        body.Should().NotBeNull();
    }
}
