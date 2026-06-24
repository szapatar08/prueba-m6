using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Handlers;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;

namespace Prueba.IntegrationTests;

/// <summary>
/// Integration tests for notification handlers using the real HandlebarsTemplateRenderer.
/// Verifies that handlers render correct templates with real template data and database-backed templates.
/// </summary>
[Collection("IntegrationTests")]
public class HandlerTemplateIntegrationTests : IAsyncLifetime
{
    private readonly PruebaWebApplicationFactory _factory;
    private readonly Guid _tenantId;
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly NoOpEmailService _emailService;
    private readonly HandlebarsTemplateRenderer _renderer;

    public HandlerTemplateIntegrationTests(PruebaWebApplicationFactory factory)
    {
        _factory = factory;
        _tenantId = factory.DefaultTenantId;

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _repository = new SqliteCompatibleRepository(context);
        _currentTenant = new TestCurrentTenantService(_tenantId);
        _emailService = new NoOpEmailService();
        _renderer = new HandlebarsTemplateRenderer(_repository, _currentTenant, new MemoryCache(new MemoryCacheOptions()));
    }

    public async Task InitializeAsync()
    {
        await SeedTemplatesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedTemplatesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Only seed if no templates exist for this tenant
        var existing = context.Set<NotificationTemplate>()
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == _tenantId)
            .ToList();

        if (existing.Count > 0) return;

        var templates = new[]
        {
            NotificationTemplate.Create(TemplateTypes.BookingCreated,
                "New Booking - {{PropertyName}}",
                "<h1>Booking Created</h1><p>Hello {{GuestName}}, booking at {{PropertyName}} from {{StartDate}} to {{EndDate}}.</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.BookingConfirmed,
                "Booking Confirmed - {{PropertyName}}",
                "<h1>Booking Confirmed</h1><p>Hello {{GuestName}}, booking at {{PropertyName}} from {{StartDate}} to {{EndDate}} for ${{TotalPrice}}. Check-in at {{CheckInTime}}.</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.BookingCancelled,
                "Booking Cancelled - {{PropertyName}}",
                "<h1>Booking Cancelled</h1><p>Hello {{GuestName}}, booking at {{PropertyName}} cancelled. {{RefundInfo}}</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.KycApproved,
                "Identity Verification Approved",
                "<h1>Approved</h1><p>Hello {{GuestName}}, your identity verification has been approved.</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.KycRejected,
                "Identity Verification Rejected",
                "<h1>Rejected</h1><p>Hello {{GuestName}}, your identity verification has been rejected. {{RejectionReason}}</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.ArrivalReminder,
                "Check-in tomorrow - {{PropertyName}}",
                "<h1>Arrival Reminder</h1><p>Hello {{GuestName}}, check-in at {{PropertyName}} is tomorrow.</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.DepartureReminder,
                "Check-out today - {{PropertyName}}",
                "<h1>Departure Reminder</h1><p>Hello {{GuestName}}, check-out at {{PropertyName}} is today.</p>",
                _tenantId),
        };

        context.Set<NotificationTemplate>().AddRange(templates);
        await context.SaveChangesAsync();
    }

    private async Task<User> CreateUserAsync(string email, string firstName, string lastName)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create(email, BCrypt.Net.BCrypt.HashPassword("TestPassword123!"), firstName, lastName, _tenantId);
        context.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<(BookingEntity Booking, Property Property)> CreateBookingAndPropertyAsync(Guid guestId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = Property.Create("Beach House", "Nice place", "Beach", "123 Ocean Dr", "Miami", "US", 100m, 4, 2, 1, Guid.NewGuid(), _tenantId);
        var booking = BookingEntity.Create(property.Id, guestId, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5), 500m, _tenantId);
        context.Add(property);
        context.Add(booking);
        await context.SaveChangesAsync();
        return (booking, property);
    }

    [Fact]
    public async Task BookingConfirmedHandler_WithRealRenderer_ShouldRenderTemplateAndSendEmail()
    {
        // Arrange
        var guest = await CreateUserAsync("confirmed.guest@test.com", "John", "Confirmed");
        var (booking, property) = await CreateBookingAndPropertyAsync(guest.Id);

        var handler = new BookingConfirmedEventHandler(_repository, _currentTenant, _emailService, _renderer);

        // Act
        await handler.HandleAsync(booking.Id, guest.Id);

        // Assert
        _emailService.SentEmails.Should().HaveCount(1);
        var email = _emailService.SentEmails[0];
        email.To.Should().Be("confirmed.guest@test.com");
        email.Subject.Should().Contain("Confirmed");
        email.Subject.Should().Contain("Beach House");
        email.Body.Should().Contain("John");
        email.Body.Should().Contain("Beach House");
        email.Body.Should().Contain("2026-12-01");
        email.Body.Should().Contain("2026-12-05");
        email.Body.Should().Contain("500");
    }

    [Fact]
    public async Task BookingCreatedHandler_WithRealRenderer_ShouldRenderTemplateAndSendEmail()
    {
        // Arrange
        var guest = await CreateUserAsync("created.guest@test.com", "Alice", "Created");
        var (booking, property) = await CreateBookingAndPropertyAsync(guest.Id);

        var handler = new BookingCreatedEventHandler(_repository, _currentTenant, _emailService, _renderer);

        // Act
        await handler.HandleAsync(booking.Id, guest.Id, property.Id, booking.StartDate, booking.EndDate);

        // Assert
        _emailService.SentEmails.Should().HaveCount(1);
        var email = _emailService.SentEmails[0];
        email.To.Should().Be("created.guest@test.com");
        email.Subject.Should().Contain("Booking");
        email.Subject.Should().Contain("Beach House");
        email.Body.Should().Contain("Alice");
        email.Body.Should().Contain("Beach House");
        email.Body.Should().Contain("2026-12-01");
    }

    [Fact]
    public async Task BookingCancelledHandler_WithRealRenderer_ShouldRenderTemplateAndSendEmail()
    {
        // Arrange
        var guest = await CreateUserAsync("cancelled.guest@test.com", "Bob", "Cancelled");
        var (booking, property) = await CreateBookingAndPropertyAsync(guest.Id);

        var handler = new BookingCancelledEventHandler(_repository, _currentTenant, _emailService, _renderer);

        // Act
        await handler.HandleAsync(booking.Id, guest.Id);

        // Assert
        _emailService.SentEmails.Should().HaveCount(1);
        var email = _emailService.SentEmails[0];
        email.To.Should().Be("cancelled.guest@test.com");
        email.Subject.Should().Contain("Cancelled");
        email.Subject.Should().Contain("Beach House");
        email.Body.Should().Contain("Bob");
        email.Body.Should().Contain("Beach House");
    }

    [Fact]
    public async Task KycApprovedHandler_WithRealRenderer_ShouldRenderKycApprovedTemplate()
    {
        // Arrange
        var user = await CreateUserAsync("kyc.approved@test.com", "Jane", "Verified");

        var handler = new KycCompletedEventHandler(_repository, _currentTenant, _emailService, _renderer);

        // Act
        await handler.HandleAsync(user.Id, "Approved");

        // Assert
        _emailService.SentEmails.Should().HaveCount(1);
        var email = _emailService.SentEmails[0];
        email.To.Should().Be("kyc.approved@test.com");
        email.Subject.Should().Contain("Approved");
        email.Body.Should().Contain("Jane");
        email.Body.Should().Contain("approved");
    }

    [Fact]
    public async Task KycRejectedHandler_WithRealRenderer_ShouldRenderKycRejectedTemplate()
    {
        // Arrange
        var user = await CreateUserAsync("kyc.rejected@test.com", "Mark", "Rejected");

        var handler = new KycCompletedEventHandler(_repository, _currentTenant, _emailService, _renderer);

        // Act
        await handler.HandleAsync(user.Id, "Rejected");

        // Assert
        _emailService.SentEmails.Should().HaveCount(1);
        var email = _emailService.SentEmails[0];
        email.To.Should().Be("kyc.rejected@test.com");
        email.Subject.Should().Contain("Rejected");
        email.Body.Should().Contain("Mark");
        email.Body.Should().Contain("rejected");
    }

    [Fact]
    public async Task Handler_WithRealRenderer_ShouldResolveGuestEmailFromUserEntity()
    {
        // Arrange — verify email is resolved from User entity, not hardcoded
        var guest = await CreateUserAsync("resolved.email@test.com", "Resolved", "User");
        var (booking, property) = await CreateBookingAndPropertyAsync(guest.Id);

        var handler = new BookingConfirmedEventHandler(_repository, _currentTenant, _emailService, _renderer);

        // Act
        await handler.HandleAsync(booking.Id, guest.Id);

        // Assert — email sent to the actual user email, not a hardcoded address
        _emailService.SentEmails.Should().HaveCount(1);
        _emailService.SentEmails[0].To.Should().Be("resolved.email@test.com");
        _emailService.SentEmails.Should().NotContain(e => e.To == "guest@example.com");
    }

    [Fact]
    public async Task Handler_WithRealRenderer_ShouldCreateNotificationInDatabase()
    {
        // Arrange
        var guest = await CreateUserAsync("notification.test@test.com", "Notify", "User");
        var (booking, property) = await CreateBookingAndPropertyAsync(guest.Id);

        var handler = new BookingConfirmedEventHandler(_repository, _currentTenant, _emailService, _renderer);

        // Act
        await handler.HandleAsync(booking.Id, guest.Id);

        // Assert — notification persisted to database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = context.Set<Notification>()
            .IgnoreQueryFilters()
            .FirstOrDefault(n => n.UserId == guest.Id && n.Type == "BookingConfirmed");

        notification.Should().NotBeNull();
        notification!.Title.Should().Be("Booking Confirmed");
        notification.TenantId.Should().Be(_tenantId);
    }
}
