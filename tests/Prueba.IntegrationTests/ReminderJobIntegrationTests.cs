using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Jobs;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;

namespace Prueba.IntegrationTests;

/// <summary>
/// Integration tests for reminder jobs using the real HandlebarsTemplateRenderer and database-backed templates.
/// Verifies that reminder jobs send emails for the correct bookings using real template rendering.
/// </summary>
[Collection("IntegrationTests")]
public class ReminderJobIntegrationTests : IAsyncLifetime
{
    private readonly PruebaWebApplicationFactory _factory;
    private readonly Guid _tenantId;

    public ReminderJobIntegrationTests(PruebaWebApplicationFactory factory)
    {
        _factory = factory;
        _tenantId = factory.DefaultTenantId;
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

        var existing = context.Set<NotificationTemplate>()
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == _tenantId)
            .ToList();

        if (existing.Count > 0) return;

        var templates = new[]
        {
            NotificationTemplate.Create(TemplateTypes.ArrivalReminder,
                "Check-in tomorrow - {{PropertyName}}",
                "<h1>Arrival Reminder</h1><p>Hello {{GuestName}}, check-in at {{PropertyName}} is tomorrow ({{StartDate}}). Time: {{CheckInTime}}. Address: {{Address}}. {{Instructions}}</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.DepartureReminder,
                "Check-out today - {{PropertyName}}",
                "<h1>Departure Reminder</h1><p>Hello {{GuestName}}, check-out at {{PropertyName}} is today ({{EndDate}}). Time: {{CheckOutTime}}. {{Instructions}}</p>",
                _tenantId),
        };

        context.Set<NotificationTemplate>().AddRange(templates);
        await context.SaveChangesAsync();
    }

    private SendArrivalRemindersJob CreateArrivalJob(
        IRepository repository,
        NoOpEmailService emailService)
    {
        return new SendArrivalRemindersJob(
            repository,
            _factory.Services.GetRequiredService<ICurrentTenant>(),
            new HandlebarsTemplateRenderer(repository, _factory.Services.GetRequiredService<ICurrentTenant>(), new MemoryCache(new MemoryCacheOptions())),
            emailService,
            NullLogger<SendArrivalRemindersJob>.Instance);
    }

    private SendDepartureRemindersJob CreateDepartureJob(
        IRepository repository,
        NoOpEmailService emailService)
    {
        return new SendDepartureRemindersJob(
            repository,
            _factory.Services.GetRequiredService<ICurrentTenant>(),
            new HandlebarsTemplateRenderer(repository, _factory.Services.GetRequiredService<ICurrentTenant>(), new MemoryCache(new MemoryCacheOptions())),
            emailService,
            NullLogger<SendDepartureRemindersJob>.Instance);
    }

    private async Task<(BookingEntity Booking, User Guest, Property Property)> CreateConfirmedBookingWithCheckInAsync(DateOnly startDate)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guest = User.Create($"guest-{Guid.NewGuid()}@test.com", BCrypt.Net.BCrypt.HashPassword("Pass123!"), "Reminder", "Guest", _tenantId);
        var property = Property.Create("Reminder Property", "Test", "Beach", "456 Test Ave", "Miami", "US", 100m, 2, 1, 1, Guid.NewGuid(), _tenantId);
        var booking = BookingEntity.Create(property.Id, guest.Id, startDate, startDate.AddDays(3), 300m, _tenantId);
        booking.Confirm();

        context.Add(guest);
        context.Add(property);
        context.Add(booking);
        await context.SaveChangesAsync();

        return (booking, guest, property);
    }

    private async Task<(BookingEntity Booking, User Guest, Property Property)> CreateConfirmedBookingWithCheckOutAsync(DateOnly endDate)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guest = User.Create($"guest-{Guid.NewGuid()}@test.com", BCrypt.Net.BCrypt.HashPassword("Pass123!"), "Departure", "Guest", _tenantId);
        var property = Property.Create("Departure Property", "Test", "Beach", "789 Test Blvd", "Orlando", "US", 150m, 3, 2, 1, Guid.NewGuid(), _tenantId);
        var booking = BookingEntity.Create(property.Id, guest.Id, endDate.AddDays(-3), endDate, 450m, _tenantId);
        booking.Confirm();

        context.Add(guest);
        context.Add(property);
        context.Add(booking);
        await context.SaveChangesAsync();

        return (booking, guest, property);
    }

    [Fact]
    public async Task ArrivalReminderJob_WithRealRenderer_ShouldSendEmailWithRenderedTemplate()
    {
        // Arrange — create a booking with check-in tomorrow
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var (booking, guest, property) = await CreateConfirmedBookingWithCheckInAsync(tomorrow);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = new SqliteCompatibleRepository(context);
        var emailService = new NoOpEmailService();
        var job = CreateArrivalJob(repository, emailService);

        // Act
        await job.ExecuteAsync(CancellationToken.None);

        // Assert
        emailService.SentEmails.Should().HaveCount(1);
        var email = emailService.SentEmails[0];
        email.To.Should().Be(guest.Email);
        email.Subject.Should().Contain("Check-in");
        email.Subject.Should().Contain(property.Name);
        email.Body.Should().Contain(guest.FirstName);
        email.Body.Should().Contain(property.Name);
        email.Body.Should().Contain(tomorrow.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task DepartureReminderJob_WithRealRenderer_ShouldSendEmailWithRenderedTemplate()
    {
        // Arrange — create a booking with check-out today
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (booking, guest, property) = await CreateConfirmedBookingWithCheckOutAsync(today);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = new SqliteCompatibleRepository(context);
        var emailService = new NoOpEmailService();
        var job = CreateDepartureJob(repository, emailService);

        // Act
        await job.ExecuteAsync(CancellationToken.None);

        // Assert
        emailService.SentEmails.Should().HaveCount(1);
        var email = emailService.SentEmails[0];
        email.To.Should().Be(guest.Email);
        email.Subject.Should().Contain("Check-out");
        email.Subject.Should().Contain(property.Name);
        email.Body.Should().Contain(guest.FirstName);
        email.Body.Should().Contain(property.Name);
        email.Body.Should().Contain(today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task ArrivalReminderJob_WithMultipleBookings_ShouldSendEmailsToAllGuests()
    {
        // Arrange — two bookings with check-in tomorrow
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var (booking1, guest1, property1) = await CreateConfirmedBookingWithCheckInAsync(tomorrow);
        var (booking2, guest2, property2) = await CreateConfirmedBookingWithCheckInAsync(tomorrow);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = new SqliteCompatibleRepository(context);
        var emailService = new NoOpEmailService();
        var job = CreateArrivalJob(repository, emailService);

        // Act
        await job.ExecuteAsync(CancellationToken.None);

        // Assert
        emailService.SentEmails.Should().HaveCount(2);
        emailService.SentEmails.Should().Contain(e => e.To == guest1.Email);
        emailService.SentEmails.Should().Contain(e => e.To == guest2.Email);
    }

    [Fact]
    public async Task ArrivalReminderJob_WithNoBookingsTomorrow_ShouldNotSendEmails()
    {
        // Arrange — booking with check-in next week (not tomorrow)
        var nextWeek = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        await CreateConfirmedBookingWithCheckInAsync(nextWeek);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = new SqliteCompatibleRepository(context);
        var emailService = new NoOpEmailService();
        var job = CreateArrivalJob(repository, emailService);

        // Act
        await job.ExecuteAsync(CancellationToken.None);

        // Assert
        emailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task DepartureReminderJob_WithNoCheckoutsToday_ShouldNotSendEmails()
    {
        // Arrange — booking with check-out tomorrow (not today)
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await CreateConfirmedBookingWithCheckOutAsync(tomorrow);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = new SqliteCompatibleRepository(context);
        var emailService = new NoOpEmailService();
        var job = CreateDepartureJob(repository, emailService);

        // Act
        await job.ExecuteAsync(CancellationToken.None);

        // Assert
        emailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task ArrivalReminderJob_WithPendingBooking_ShouldNotSendEmail()
    {
        // Arrange — booking for tomorrow but NOT confirmed
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guest = User.Create($"pending-{Guid.NewGuid()}@test.com", BCrypt.Net.BCrypt.HashPassword("Pass123!"), "Pending", "Guest", _tenantId);
        var property = Property.Create("Pending Property", "Test", "Beach", "000 Pending St", "Miami", "US", 100m, 2, 1, 1, Guid.NewGuid(), _tenantId);
        var booking = BookingEntity.Create(property.Id, guest.Id, tomorrow, tomorrow.AddDays(3), 300m, _tenantId);
        // NOT calling Confirm() — stays Pending

        context.Add(guest);
        context.Add(property);
        context.Add(booking);
        await context.SaveChangesAsync();

        var repository = new SqliteCompatibleRepository(context);
        var emailService = new NoOpEmailService();
        var job = CreateArrivalJob(repository, emailService);

        // Act
        await job.ExecuteAsync(CancellationToken.None);

        // Assert
        emailService.SentEmails.Should().BeEmpty();
    }
}
