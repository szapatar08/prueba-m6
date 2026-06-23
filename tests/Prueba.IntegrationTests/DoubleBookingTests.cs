using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prueba.Infrastructure.Data;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.KYC.Entities;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.IntegrationTests;

/// <summary>
/// Integration tests for the double-booking prevention mechanism.
/// </summary>
[Collection("IntegrationTests")]
public class DoubleBookingTests : IAsyncLifetime
{
    private readonly PruebaWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _tenantId;
    private readonly Guid _ownerId;
    private readonly Guid _guestId;
    private readonly Guid _propertyId;
    private string _ownerToken = string.Empty;
    private string _guestToken = string.Empty;

    public DoubleBookingTests(PruebaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _tenantId = factory.DefaultTenantId;
        _ownerId = Guid.NewGuid();
        _guestId = Guid.NewGuid();
        _propertyId = Guid.NewGuid();
    }

    public async Task InitializeAsync()
    {
        // Clear all data before each test for isolation
        await ClearDatabaseAsync();
        await SeedTestDataAsync();
        _ownerToken = TestTokenGenerator.GenerateOwnerToken(_ownerId, "owner@test.com", _tenantId);
        _guestToken = TestTokenGenerator.GenerateGuestToken(_guestId, "guest@test.com", _tenantId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateBooking_WithSameDatesAsConfirmedBooking_ShouldReturn409()
    {
        await CreateConfirmedBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m);

        var response = await CreateBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 600m);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateBooking_WithPartialOverlapAtStart_ShouldReturn409()
    {
        await CreateConfirmedBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m);

        var response = await CreateBookingAsync(
            new DateOnly(2026, 8, 8), new DateOnly(2026, 8, 12), 400m);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateBooking_WithPartialOverlapAtEnd_ShouldReturn409()
    {
        await CreateConfirmedBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m);

        var response = await CreateBookingAsync(
            new DateOnly(2026, 8, 13), new DateOnly(2026, 8, 18), 500m);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateBooking_WithFullOverlap_ShouldReturn409()
    {
        await CreateConfirmedBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m);

        var response = await CreateBookingAsync(
            new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 20), 1500m);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateBooking_AdjacentToExistingBooking_ShouldSucceed()
    {
        await CreateConfirmedBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m);

        var response = await CreateBookingAsync(
            new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 20), 500m);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBooking_NonOverlappingDates_ShouldSucceed()
    {
        await CreateConfirmedBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m);

        var response = await CreateBookingAsync(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 400m);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBooking_WithPendingBookingOverlap_ShouldSucceed()
    {
        var pendingResponse = await CreateBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m);
        pendingResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await CreateBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 600m);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBooking_OnDifferentProperty_ShouldNotBlock()
    {
        var otherPropertyId = Guid.NewGuid();
        await SeedPropertyAsync(otherPropertyId);

        await CreateConfirmedBookingAsync(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestToken);
        var command = new
        {
            propertyId = otherPropertyId,
            startDate = "2026-08-10",
            endDate = "2026-08-15",
            totalPrice = 500m
        };
        var response = await _client.PostAsJsonAsync("/api/bookings", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBooking_ConcurrentRequestsForSameDates_ShouldDetectOverlap()
    {
        // This test verifies that the overlap detection formula correctly rejects
        // a second booking when a CONFIRMED booking already exists for the same dates.
        //
        // NOTE: True concurrent SERIALIZABLE isolation requires PostgreSQL.
        // SQLite in-memory shares a single connection, so we test the overlap
        // detection logic sequentially. The existing CreateBooking_WithSameDates
        // tests already verify this via the API. This test adds verification that
        // the database never ends up with conflicting confirmed bookings.

        // Arrange: seed a second guest
        var guest2Id = Guid.NewGuid();
        var guest2 = User.Create("guest2@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest2", "User", _tenantId);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Entry(guest2).Property("Id").CurrentValue = guest2Id;
            var guestRole = context.Set<Role>().First(r => r.Name == "Guest" && r.TenantId == _tenantId);
            var guest2UserRole = UserRole.Create(guest2Id, guestRole.Id, _tenantId);
            context.Add(guest2);
            context.Add(guest2UserRole);

            var kyc2 = KycValidation.Create(guest2Id, "passport", _tenantId);
            kyc2.Approve("Guest2 User", "DOC456", new DateTime(1992, 5, 15));
            context.Add(kyc2);
            await context.SaveChangesAsync();
        }

        var guest2Token = TestTokenGenerator.GenerateGuestToken(guest2Id, "guest2@test.com", _tenantId);

        var startDate = new DateOnly(2026, 9, 1);
        var endDate = new DateOnly(2026, 9, 5);

        // Act: first guest creates and CONFIRMS a booking
        await CreateConfirmedBookingAsync(startDate, endDate, 400m);

        // Act: second guest tries the same dates — must be rejected by overlap detection
        var response2 = await CreateBookingAsAsync(guest2Token, startDate, endDate, 450m);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "the overlap detection must reject a second booking for the same confirmed dates");

        // Verify database consistency: exactly one booking for these dates
        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingsForDates = await verifyContext.Set<BookingEntity>()
            .IgnoreQueryFilters()
            .Where(b => b.PropertyId == _propertyId
                && b.StartDate == startDate
                && b.EndDate == endDate)
            .CountAsync();
        bookingsForDates.Should().Be(1, "exactly one booking should exist for the contested dates");
    }

    // === Helper Methods ===

    private async Task ClearDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Use ExecuteDelete for reliable cleanup
        await context.Set<BookingEntity>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<WishlistItem>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<KycDocument>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<KycValidation>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<PropertyImage>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<Availability>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<Property>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<UserRole>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<Role>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<User>().IgnoreQueryFilters().ExecuteDeleteAsync();
    }

    private async Task SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = User.Create("owner@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Owner", "User", _tenantId);
        context.Entry(owner).Property("Id").CurrentValue = _ownerId;

        var ownerRole = Role.Create("Owner", _tenantId);
        var ownerUserRole = UserRole.Create(_ownerId, ownerRole.Id, _tenantId);

        var guest = User.Create("guest@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "User", _tenantId);
        context.Entry(guest).Property("Id").CurrentValue = _guestId;

        var guestRole = Role.Create("Guest", _tenantId);
        var guestUserRole = UserRole.Create(_guestId, guestRole.Id, _tenantId);

        context.Add(owner);
        context.Add(ownerRole);
        context.Add(ownerUserRole);
        context.Add(guest);
        context.Add(guestRole);
        context.Add(guestUserRole);

        // Approve KYC for guest
        var kyc = KycValidation.Create(_guestId, "passport", _tenantId);
        kyc.Approve("Guest User", "DOC123", new DateTime(1990, 1, 1));
        context.Add(kyc);

        await SeedPropertyAsync(_propertyId);
        await context.SaveChangesAsync();
    }

    private async Task SeedPropertyAsync(Guid propertyId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = Property.Create(
            "Test Property", "A nice place", "123 Main St",
            "123 Main St", "Buenos Aires", "Argentina",
            100m, 4, 2, 1, _ownerId, _tenantId);

        context.Entry(property).Property("Id").CurrentValue = propertyId;
        context.Add(property);
        await context.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> CreateBookingAsync(
        DateOnly startDate, DateOnly endDate, decimal totalPrice)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestToken);

        return await _client.PostAsJsonAsync("/api/bookings", new
        {
            propertyId = _propertyId,
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            totalPrice
        });
    }

    private async Task CreateConfirmedBookingAsync(
        DateOnly startDate, DateOnly endDate, decimal totalPrice)
    {
        var createResponse = await CreateBookingAsync(startDate, endDate, totalPrice);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var bookingBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = bookingBody.GetProperty("id").GetGuid();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _ownerToken);
        var confirmResponse = await _client.PutAsync($"/api/bookings/{bookingId}/confirm", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> CreateBookingAsAsync(
        string token, DateOnly startDate, DateOnly endDate, decimal totalPrice)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return await client.PostAsJsonAsync("/api/bookings", new
        {
            propertyId = _propertyId,
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            totalPrice
        });
    }
}
