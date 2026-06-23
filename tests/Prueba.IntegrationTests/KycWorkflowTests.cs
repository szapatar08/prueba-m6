using System.Net;
using System.Net.Http.Json;
using System.Text;
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
/// Integration tests for the KYC verification workflow.
/// Tests the KYC gate: first booking blocked without KYC, allowed after KYC approval.
/// </summary>
[Collection("IntegrationTests")]
public class KycWorkflowTests : IAsyncLifetime
{
    private readonly PruebaWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _tenantId;
    private readonly Guid _ownerId;
    private readonly Guid _guestId;
    private readonly Guid _propertyId;
    private string _guestToken = string.Empty;
    private string _ownerToken = string.Empty;

    public KycWorkflowTests(PruebaWebApplicationFactory factory)
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
        await ClearDatabaseAsync();
        await SeedTestDataAsync();
        _guestToken = TestTokenGenerator.GenerateGuestToken(_guestId, "guest@test.com", _tenantId);
        _ownerToken = TestTokenGenerator.GenerateOwnerToken(_ownerId, "owner@test.com", _tenantId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FirstBooking_WithoutKyc_ShouldBeBlocked()
    {
        // Arrange — guest has no KYC and no previous bookings
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestToken);

        var command = new
        {
            propertyId = _propertyId,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/bookings", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("KYC");
    }

    [Fact]
    public async Task FirstBooking_AfterKycUpload_ShouldStillBeBlockedUntilApproved()
    {
        // Arrange — upload KYC document (status = Pending)
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestToken);

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake-pdf-content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        using var formData = new MultipartFormDataContent();
        formData.Add(fileContent, "file", "passport.pdf");

        var uploadResponse = await _client.PostAsync("/api/kyc/documents", formData);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — try to book while KYC is pending
        var bookingCommand = new
        {
            propertyId = _propertyId,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        var bookingResponse = await _client.PostAsJsonAsync("/api/bookings", bookingCommand);

        // Assert — should still be blocked (KYC is pending, not approved)
        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FirstBooking_AfterKycApproval_ShouldSucceed()
    {
        // Arrange — create and approve KYC
        await ApproveKycAsync();

        // Act — try to book after KYC approval
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestToken);

        var command = new
        {
            propertyId = _propertyId,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        var response = await _client.PostAsJsonAsync("/api/bookings", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Status is returned as enum integer (0 = Pending)
        body.GetProperty("status").GetInt32().Should().Be(0); // KycStatus.Pending
    }

    [Fact]
    public async Task SubsequentBooking_WithoutKyc_ShouldSucceed()
    {
        // Arrange — approve KYC and create first booking
        await ApproveKycAsync();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestToken);

        var firstBooking = new
        {
            propertyId = _propertyId,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 400m
        };
        var firstResponse = await _client.PostAsJsonAsync("/api/bookings", firstBooking);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Now remove KYC approval (simulate revoked KYC)
        await RevokeKycAsync();

        // Act — second booking should succeed because guest has previous bookings
        var secondBooking = new
        {
            propertyId = _propertyId,
            startDate = "2026-09-10",
            endDate = "2026-09-15",
            totalPrice = 500m
        };
        var response = await _client.PostAsJsonAsync("/api/bookings", secondBooking);

        // Assert — subsequent booking allowed even without KYC (has previous bookings)
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task KycUpload_WithValidDocument_ShouldReturnCreated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestToken);

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake-pdf-content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        using var formData = new MultipartFormDataContent();
        formData.Add(fileContent, "file", "passport.pdf");

        // Act
        var response = await _client.PostAsync("/api/kyc/documents", formData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Status is returned as enum (integer 0 = Pending)
        body.GetProperty("status").GetInt32().Should().Be(0); // KycStatus.Pending = 0
    }

    [Fact]
    public async Task KycStatus_WithNoKyc_ShouldReturnNull()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _guestToken);

        // Act
        var response = await _client.GetAsync("/api/kyc/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task KycUpload_Unauthenticated_ShouldReturn401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake-pdf-content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        using var formData = new MultipartFormDataContent();
        formData.Add(fileContent, "file", "passport.pdf");

        // Act
        var response = await _client.PostAsync("/api/kyc/documents", formData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private async Task SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = User.Create("owner@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Owner", "User", _tenantId);
        context.Entry(owner).Property("Id").CurrentValue = _ownerId;

        var guest = User.Create("guest@test.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "User", _tenantId);
        context.Entry(guest).Property("Id").CurrentValue = _guestId;

        var ownerRole = Role.Create("Owner", _tenantId);
        var guestRole = Role.Create("Guest", _tenantId);

        context.Add(owner);
        context.Add(guest);
        context.Add(ownerRole);
        context.Add(guestRole);
        context.Add(UserRole.Create(_ownerId, ownerRole.Id, _tenantId));
        context.Add(UserRole.Create(_guestId, guestRole.Id, _tenantId));

        var property = Property.Create(
            "Test Property", "A nice place", "123 Main St",
            "123 Main St", "Buenos Aires", "Argentina",
            100m, 4, 2, 1, _ownerId, _tenantId);
        context.Entry(property).Property("Id").CurrentValue = _propertyId;
        context.Add(property);

        await context.SaveChangesAsync();
    }

    private async Task ApproveKycAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var kyc = KycValidation.Create(_guestId, "passport", _tenantId);
        kyc.Approve("Test User", "DOC123456", new DateTime(1990, 1, 1));

        context.Add(kyc);
        await context.SaveChangesAsync();
    }

    private async Task RevokeKycAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var kyc = await context.Set<KycValidation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.UserId == _guestId && k.TenantId == _tenantId);

        if (kyc is not null)
        {
            context.Remove(kyc);
            await context.SaveChangesAsync();
        }
    }
}
