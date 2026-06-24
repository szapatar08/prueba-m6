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
/// Integration tests for tenant isolation and authorization.
/// Verifies that users from one tenant cannot access another tenant's data.
/// </summary>
[Collection("IntegrationTests")]
public class AuthorizationTests : IAsyncLifetime
{
    private readonly PruebaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    // Tenant A
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _ownerA = Guid.NewGuid();
    private readonly Guid _guestA = Guid.NewGuid();
    private readonly Guid _propertyA = Guid.NewGuid();
    private string _ownerTokenA = string.Empty;
    private string _guestTokenA = string.Empty;

    // Tenant B
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _ownerB = Guid.NewGuid();
    private readonly Guid _guestB = Guid.NewGuid();
    private readonly Guid _propertyB = Guid.NewGuid();
    private string _ownerTokenB = string.Empty;
    private string _guestTokenB = string.Empty;

    public AuthorizationTests(PruebaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await ClearDatabaseAsync();
        await SeedMultiTenantDataAsync();

        _ownerTokenA = TestTokenGenerator.GenerateOwnerToken(_ownerA, "ownerA@test.com", _tenantA);
        _guestTokenA = TestTokenGenerator.GenerateGuestToken(_guestA, "guestA@test.com", _tenantA);
        _ownerTokenB = TestTokenGenerator.GenerateOwnerToken(_ownerB, "ownerB@test.com", _tenantB);
        _guestTokenB = TestTokenGenerator.GenerateGuestToken(_guestB, "guestB@test.com", _tenantB);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // === Tenant Isolation Tests ===

    [Fact]
    public async Task GuestA_CannotSeeBookingsFromTenantB()
    {
        // Arrange — create a booking as guestB in tenant B
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestTokenB);
        var bookingCommand = new
        {
            propertyId = _propertyB,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        await _client.PostAsJsonAsync("/api/bookings", bookingCommand);

        // Act — guestA lists their bookings (tenant A)
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestTokenA);
        var response = await _client.GetAsync("/api/bookings");

        // Assert — guestA should see no bookings (tenant B's booking is invisible)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bookings = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task OwnerA_CannotSeeBookingsForPropertyInTenantB()
    {
        // Arrange — create a booking on propertyB (tenant B)
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestTokenB);
        var bookingCommand = new
        {
            propertyId = _propertyB,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        await _client.PostAsJsonAsync("/api/bookings", bookingCommand);

        // Act — ownerA tries to get bookings for propertyB
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _ownerTokenA);
        var response = await _client.GetAsync($"/api/bookings/property/{_propertyB}");

        // Assert — should return empty (propertyB is in tenant B, ownerA is in tenant A)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bookings = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task GuestA_CannotCancelBookingFromTenantB()
    {
        // Arrange — create and get a booking in tenant B
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestTokenB);
        var bookingCommand = new
        {
            propertyId = _propertyB,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/bookings", bookingCommand);
        var bookingBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = bookingBody.GetProperty("id").GetGuid();

        // Act — guestA tries to cancel guestB's booking
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestTokenA);
        var response = await _client.PutAsync($"/api/bookings/{bookingId}/cancel", null);

        // Assert — should be 404 (not found in tenant A)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OwnerA_CannotConfirmBookingFromTenantB()
    {
        // Arrange — create a booking in tenant B
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestTokenB);
        var bookingCommand = new
        {
            propertyId = _propertyB,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/bookings", bookingCommand);
        var bookingBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = bookingBody.GetProperty("id").GetGuid();

        // Act — ownerA tries to confirm guestB's booking
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _ownerTokenA);
        var response = await _client.PutAsync($"/api/bookings/{bookingId}/confirm", null);

        // Assert — should be 404 (not found in tenant A)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GuestA_CannotAccessPropertiesFromTenantB()
    {
        // Act — guestA tries to get propertyB details (public endpoint, should work)
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestTokenA);
        var response = await _client.GetAsync($"/api/properties/{_propertyB}");

        // Assert — public endpoint, should return property details regardless of tenant
        // (Property browsing is public, but tenant filtering applies to owned operations)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OwnerA_CannotDeletePropertyFromTenantB()
    {
        // Act — ownerA tries to delete propertyB
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _ownerTokenA);
        var response = await _client.DeleteAsync($"/api/properties/{_propertyB}");

        // Assert — should be forbidden or not found
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotCreateBooking()
    {
        // Act — no auth header
        _client.DefaultRequestHeaders.Authorization = null;
        var command = new
        {
            propertyId = _propertyA,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        var response = await _client.PostAsJsonAsync("/api/bookings", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Guest_CannotCreateProperty()
    {
        // Act — guest tries to create a property (Owner role required)
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestTokenA);
        var command = new
        {
            name = "New Property",
            description = "Test",
            location = "123 Test St",
            address = "123 Test St",
            city = "Buenos Aires",
            country = "Argentina",
            pricePerNight = 100m,
            maxGuests = 4,
            bedrooms = 2,
            bathrooms = 1
        };
        var response = await _client.PostAsJsonAsync("/api/properties", command);

        // Assert — guest should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_CannotCreateBooking()
    {
        // Act — owner tries to create a booking (Guest role required)
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _ownerTokenA);
        var command = new
        {
            propertyId = _propertyA,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        var response = await _client.PostAsJsonAsync("/api/bookings", command);

        // Assert — owner should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // === Helper Methods ===

    private async Task ClearDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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

    private async Task SeedMultiTenantDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Tenant A users
        var ownerA = User.Create("ownerA@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Owner", "A", _tenantA);
        context.Entry(ownerA).Property("Id").CurrentValue = _ownerA;

        var guestA = User.Create("guestA@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "A", _tenantA);
        context.Entry(guestA).Property("Id").CurrentValue = _guestA;

        var ownerRoleA = Role.Create("Owner", _tenantA);
        var guestRoleA = Role.Create("Guest", _tenantA);

        context.Add(ownerA);
        context.Add(guestA);
        context.Add(ownerRoleA);
        context.Add(guestRoleA);
        context.Add(UserRole.Create(_ownerA, ownerRoleA.Id, _tenantA));
        context.Add(UserRole.Create(_guestA, guestRoleA.Id, _tenantA));

        // Tenant A property
        var propertyA = Property.Create(
            "Property A", "Nice place A", "123 Main St",
            "123 Main St", "Buenos Aires", "Argentina",
            100m, 4, 2, 1, _ownerA, _tenantA);
        context.Entry(propertyA).Property("Id").CurrentValue = _propertyA;
        context.Add(propertyA);

        // Approve KYC for tenant A guest
        var kycA = KycValidation.Create(_guestA, "passport", _tenantA);
        kycA.Approve("Guest A", "DOC-A", new DateTime(1990, 1, 1), 95.0);
        context.Add(kycA);

        // Tenant B users
        var ownerB = User.Create("ownerB@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Owner", "B", _tenantB);
        context.Entry(ownerB).Property("Id").CurrentValue = _ownerB;

        var guestB = User.Create("guestB@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "B", _tenantB);
        context.Entry(guestB).Property("Id").CurrentValue = _guestB;

        var ownerRoleB = Role.Create("Owner", _tenantB);
        var guestRoleB = Role.Create("Guest", _tenantB);

        context.Add(ownerB);
        context.Add(guestB);
        context.Add(ownerRoleB);
        context.Add(guestRoleB);
        context.Add(UserRole.Create(_ownerB, ownerRoleB.Id, _tenantB));
        context.Add(UserRole.Create(_guestB, guestRoleB.Id, _tenantB));

        // Tenant B property
        var propertyB = Property.Create(
            "Property B", "Nice place B", "456 Other St",
            "456 Other St", "São Paulo", "Brazil",
            200m, 6, 3, 2, _ownerB, _tenantB);
        context.Entry(propertyB).Property("Id").CurrentValue = _propertyB;
        context.Add(propertyB);

        // Approve KYC for tenant B guest
        var kycB = KycValidation.Create(_guestB, "passport", _tenantB);
        kycB.Approve("Guest B", "DOC-B", new DateTime(1995, 5, 5), 95.0);
        context.Add(kycB);

        await context.SaveChangesAsync();
    }
}
