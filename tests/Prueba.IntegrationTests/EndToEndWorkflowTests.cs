using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.KYC.Entities;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.IntegrationTests;

/// <summary>
/// End-to-end workflow integration test.
/// Tests the complete happy path: Register → Login → Create Property → Upload KYC → Book → Confirm → Complete.
/// Uses WebApplicationFactory with SQLite in-memory database.
/// </summary>
[Collection("IntegrationTests")]
public class EndToEndWorkflowTests : IAsyncLifetime
{
    private readonly PruebaWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _tenantId;

    public EndToEndWorkflowTests(PruebaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _tenantId = factory.DefaultTenantId;
    }

    public async Task InitializeAsync()
    {
        await ClearDatabaseAsync();
        await SeedNotificationTemplatesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

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
        await context.Set<Notification>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<NotificationTemplate>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<UserRole>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<Role>().IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Set<User>().IgnoreQueryFilters().ExecuteDeleteAsync();

        // Clear captured emails
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>() as NoOpEmailService;
        emailService?.Clear();
    }

    private async Task SeedNotificationTemplatesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var templates = new[]
        {
            NotificationTemplate.Create(TemplateTypes.BookingCreated,
                "New Booking - {{PropertyName}}",
                "<h1>Booking Created</h1><p>Hello {{GuestName}}, your booking at {{PropertyName}} from {{StartDate}} to {{EndDate}} has been created.</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.BookingConfirmed,
                "Booking Confirmed - {{PropertyName}}",
                "<h1>Booking Confirmed</h1><p>Hello {{GuestName}}, your booking at {{PropertyName}} from {{StartDate}} to {{EndDate}} for ${{TotalPrice}} has been confirmed. Check-in at {{CheckInTime}}.</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.BookingCancelled,
                "Booking Cancelled - {{PropertyName}}",
                "<h1>Booking Cancelled</h1><p>Hello {{GuestName}}, your booking at {{PropertyName}} has been cancelled. {{RefundInfo}}</p>",
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
                "<h1>Arrival Reminder</h1><p>Hello {{GuestName}}, your check-in at {{PropertyName}} is tomorrow ({{StartDate}}). Check-in time: {{CheckInTime}}. Address: {{Address}}. {{Instructions}}</p>",
                _tenantId),
            NotificationTemplate.Create(TemplateTypes.DepartureReminder,
                "Check-out today - {{PropertyName}}",
                "<h1>Departure Reminder</h1><p>Hello {{GuestName}}, your check-out at {{PropertyName}} is today ({{EndDate}}). Check-out time: {{CheckOutTime}}. {{Instructions}}</p>",
                _tenantId),
        };

        context.Set<NotificationTemplate>().AddRange(templates);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task FullHappyPath_RegisterLoginCreatePropertyUploadKycBookConfirmComplete()
    {
        // =====================================================================
        // STEP 1: Register Owner
        // =====================================================================
        var ownerEmail = $"owner-{Guid.NewGuid()}@test.com";
        var ownerPassword = "SecurePassword123!";

        // Pre-generate token for registration (sets tenant context)
        var ownerSeedToken = TestTokenGenerator.GenerateOwnerToken(Guid.NewGuid(), ownerEmail, _tenantId);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerSeedToken);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = ownerEmail,
            password = ownerPassword,
            firstName = "John",
            lastName = "Owner"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var registeredOwnerId = registerBody.GetProperty("id").GetGuid();

        // Assign Owner role to the registered user (API defaults to Guest)
        await AssignOwnerRoleAsync(registeredOwnerId);

        // =====================================================================
        // STEP 2: Login Owner
        // =====================================================================
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ownerEmail,
            password = ownerPassword
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ownerJwt = loginBody.GetProperty("token").GetString();
        ownerJwt.Should().NotBeNullOrEmpty();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerJwt);

        // =====================================================================
        // STEP 3: Create Property (Owner)
        // =====================================================================
        var createPropertyCommand = new
        {
            name = "Beautiful Apartment in Buenos Aires",
            description = "A modern apartment in the heart of Palermo",
            location = "Palermo, Buenos Aires",
            address = "Av. Santa Fe 1234",
            city = "Buenos Aires",
            country = "Argentina",
            pricePerNight = 150.00m,
            maxGuests = 4,
            bedrooms = 2,
            bathrooms = 1
        };

        var propertyResponse = await _client.PostAsJsonAsync("/api/properties", createPropertyCommand);
        propertyResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var propertyBody = await propertyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var propertyId = propertyBody.GetProperty("id").GetGuid();
        propertyId.Should().NotBeEmpty();

        // =====================================================================
        // STEP 4: Register Guest
        // =====================================================================
        var guestUserId = Guid.NewGuid();
        var guestEmail = $"guest-{Guid.NewGuid()}@test.com";
        var guestToken = TestTokenGenerator.GenerateGuestToken(guestUserId, guestEmail, _tenantId);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestToken);

        var guestRegisterCommand = new
        {
            email = guestEmail,
            password = "GuestPassword123!",
            firstName = "Jane",
            lastName = "Guest"
        };

        var guestRegisterResponse = await _client.PostAsJsonAsync("/api/auth/register", guestRegisterCommand);
        guestRegisterResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var guestRegisterBody = await guestRegisterResponse.Content.ReadFromJsonAsync<JsonElement>();
        var registeredGuestId = guestRegisterBody.GetProperty("id").GetGuid();

        // Login guest
        var guestLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = guestEmail,
            password = "GuestPassword123!"
        });
        guestLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var guestLoginBody = await guestLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var guestJwt = guestLoginBody.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestJwt);

        // =====================================================================
        // STEP 5: First Booking Without KYC → Should Be Blocked
        // =====================================================================
        var blockedBookingCommand = new
        {
            propertyId,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 600.00m
        };

        var blockedResponse = await _client.PostAsJsonAsync("/api/bookings", blockedBookingCommand);
        blockedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var blockedBody = await blockedResponse.Content.ReadFromJsonAsync<JsonElement>();
        blockedBody.GetProperty("error").GetString().Should().Contain("KYC");

        // =====================================================================
        // STEP 6: Upload KYC Document
        // =====================================================================
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake-pdf-passport-content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        using var formData = new MultipartFormDataContent();
        formData.Add(fileContent, "file", "passport.pdf");

        var kycUploadResponse = await _client.PostAsync("/api/kyc/documents", formData);
        kycUploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var kycBody = await kycUploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var kycValidationId = kycBody.GetProperty("validationId").GetGuid();

        // =====================================================================
        // STEP 7: Approve KYC (directly in DB — simulates background job)
        // =====================================================================
        await ApproveKycInDatabaseAsync(kycValidationId, registeredGuestId);

        // =====================================================================
        // STEP 8: Create Booking After KYC Approval → Should Succeed
        // =====================================================================
        var bookingCommand = new
        {
            propertyId,
            startDate = "2026-09-01",
            endDate = "2026-09-05",
            totalPrice = 600.00m
        };

        var bookingResponse = await _client.PostAsJsonAsync("/api/bookings", bookingCommand);
        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookingBody = await bookingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = bookingBody.GetProperty("id").GetGuid();
        // Status is returned as enum integer (0 = Pending)
        bookingBody.GetProperty("status").GetInt32().Should().Be(0); // BookingStatus.Pending
        bookingBody.GetProperty("propertyId").GetGuid().Should().Be(propertyId);
        bookingBody.GetProperty("guestId").GetGuid().Should().Be(registeredGuestId);

        // =====================================================================
        // STEP 9: Confirm Booking (Owner)
        // =====================================================================
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerJwt);

        var confirmResponse = await _client.PutAsync($"/api/bookings/{bookingId}/confirm", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        confirmBody.GetProperty("status").GetInt32().Should().Be(1); // BookingStatus.Confirmed

        // =====================================================================
        // STEP 10: Complete Booking (Owner)
        // =====================================================================
        var completeResponse = await _client.PutAsync($"/api/bookings/{bookingId}/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completeBody = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        completeBody.GetProperty("status").GetInt32().Should().Be(2); // BookingStatus.Completed

        // =====================================================================
        // STEP 11: Verify Final State — Guest Lists Bookings
        // =====================================================================
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestJwt);

        var listResponse = await _client.GetAsync("/api/bookings");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bookings = await listResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        bookings.Should().HaveCount(1);
        bookings![0].GetProperty("status").GetInt32().Should().Be(2); // BookingStatus.Completed
        bookings[0].GetProperty("id").GetGuid().Should().Be(bookingId);

        // =====================================================================
        // STEP 12: Verify Property Appears in Catalog
        // =====================================================================
        _client.DefaultRequestHeaders.Authorization = null; // Public endpoint
        var catalogResponse = await _client.GetAsync("/api/properties");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<JsonElement>();
        // Catalog should contain our property
    }

    [Fact]
    public async Task CancelBooking_Workflow()
    {
        // Register + Login
        var (guestJwt, guestId) = await RegisterAndLoginGuestAsync();

        // Approve KYC
        await ApproveKycForUserAsync(guestId);

        // Create property
        var (ownerJwt, propertyId) = await CreateOwnerAndPropertyAsync();

        // Create booking
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestJwt);
        var bookingResponse = await _client.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            startDate = "2026-10-01",
            endDate = "2026-10-05",
            totalPrice = 400m
        });
        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookingBody = await bookingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = bookingBody.GetProperty("id").GetGuid();

        // Cancel booking
        var cancelResponse = await _client.PutAsync($"/api/bookings/{bookingId}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelBody = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>();
        cancelBody.GetProperty("status").GetInt32().Should().Be(3); // BookingStatus.Cancelled
    }

    [Fact]
    public async Task Wishlist_Workflow()
    {
        // Register + Login guest
        var (guestJwt, _) = await RegisterAndLoginGuestAsync();

        // Create property
        var (_, propertyId) = await CreateOwnerAndPropertyAsync();

        // Add to wishlist
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestJwt);
        var addResponse = await _client.PostAsync($"/api/wishlist/{propertyId}", null);
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get wishlist
        var getResponse = await _client.GetAsync("/api/wishlist");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var wishlist = await getResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        wishlist.Should().HaveCount(1);

        // Remove from wishlist
        var removeResponse = await _client.DeleteAsync($"/api/wishlist/{propertyId}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify empty
        var emptyResponse = await _client.GetAsync("/api/wishlist");
        var emptyWishlist = await emptyResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        emptyWishlist.Should().BeEmpty();
    }

    [Fact]
    public async Task EmailFlow_RegisterBookConfirm_ShouldSendConfirmationEmail()
    {
        // =====================================================================
        // STEP 1: Register Guest
        // =====================================================================
        var (guestJwt, guestId) = await RegisterAndLoginGuestAsync();

        // =====================================================================
        // STEP 2: Approve KYC for Guest
        // =====================================================================
        await ApproveKycForUserAsync(guestId);

        // =====================================================================
        // STEP 3: Create Owner + Property
        // =====================================================================
        var (ownerJwt, propertyId) = await CreateOwnerAndPropertyAsync();

        // =====================================================================
        // STEP 4: Create Booking (Guest)
        // =====================================================================
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestJwt);

        var bookingResponse = await _client.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            startDate = "2026-11-01",
            endDate = "2026-11-05",
            totalPrice = 600.00m
        });
        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookingBody = await bookingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = bookingBody.GetProperty("id").GetGuid();

        // =====================================================================
        // STEP 5: Verify BookingCreated email was sent
        // =====================================================================
        {
            var emailSvc = _factory.Services.GetRequiredService<IEmailService>() as NoOpEmailService;
            var createdEmail = emailSvc!.SentEmails.FirstOrDefault(e => e.Subject.Contains("Booking"));
            createdEmail.To.Should().NotBeNullOrEmpty("because the BookingCreated handler should send an email");
        }

        // =====================================================================
        // STEP 6: Confirm Booking (Owner)
        // =====================================================================
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerJwt);

        var confirmResponse = await _client.PutAsync($"/api/bookings/{bookingId}/confirm", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // =====================================================================
        // STEP 7: Verify BookingConfirmed email was sent
        // =====================================================================
        {
            var emailSvc = _factory.Services.GetRequiredService<IEmailService>() as NoOpEmailService;
            var confirmedEmail = emailSvc!.SentEmails.FirstOrDefault(e => e.Subject.Contains("Confirmed"));
            confirmedEmail.Subject.Should().Contain("Confirmed");
            confirmedEmail.Body.Should().Contain("Confirmed");
        }
    }

    // === Helper Methods ===

    private async Task<(string Jwt, Guid UserId)> RegisterAndLoginGuestAsync()
    {
        var email = $"guest-{Guid.NewGuid()}@test.com";
        var password = "GuestPassword123!";
        var userId = Guid.NewGuid();
        var token = TestTokenGenerator.GenerateGuestToken(userId, email, _tenantId);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password,
            firstName = "Test",
            lastName = "Guest"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = body.GetProperty("token").GetString()!;
        var registeredId = body.GetProperty("userId").GetGuid();

        return (jwt, registeredId);
    }

    private async Task<(string Jwt, Guid PropertyId)> CreateOwnerAndPropertyAsync()
    {
        var email = $"owner-{Guid.NewGuid()}@test.com";
        var password = "OwnerPassword123!";

        // Register with tenant context
        var seedToken = TestTokenGenerator.GenerateOwnerToken(Guid.NewGuid(), email, _tenantId);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", seedToken);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password,
            firstName = "Test",
            lastName = "Owner"
        });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registerBody.GetProperty("id").GetGuid();

        // Assign Owner role
        await AssignOwnerRoleAsync(userId);

        // Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = loginBody.GetProperty("token").GetString()!;

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var propertyResponse = await _client.PostAsJsonAsync("/api/properties", new
        {
            name = $"Property-{Guid.NewGuid()}",
            description = "Test property",
            location = "Test Location",
            address = "123 Test St",
            city = "Buenos Aires",
            country = "Argentina",
            pricePerNight = 100m,
            maxGuests = 4,
            bedrooms = 2,
            bathrooms = 1
        });

        var propertyBody = await propertyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var propertyId = propertyBody.GetProperty("id").GetGuid();

        return (jwt, propertyId);
    }

    private async Task AssignOwnerRoleAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find or create Owner role
        var ownerRole = await context.Set<Role>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == "Owner" && r.TenantId == _tenantId);

        if (ownerRole is null)
        {
            ownerRole = Role.Create("Owner", _tenantId);
            context.Add(ownerRole);
            await context.SaveChangesAsync();
        }

        // Assign Owner role to user
        var userRole = UserRole.Create(userId, ownerRole.Id, _tenantId);
        context.Add(userRole);
        await context.SaveChangesAsync();
    }

    private async Task ApproveKycForUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var kyc = KycValidation.Create(userId, "passport", _tenantId);
        kyc.Approve("Test User", "DOC123", new DateTime(1990, 1, 1), 95.0);
        context.Add(kyc);
        await context.SaveChangesAsync();
    }

    private async Task ApproveKycInDatabaseAsync(Guid kycValidationId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var kyc = await context.Set<KycValidation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == kycValidationId);

        if (kyc is null)
        {
            // Create a new approved KYC if the record wasn't found
            kyc = KycValidation.Create(userId, "passport", _tenantId);
            kyc.Approve("Test User", "DOC123", new DateTime(1990, 1, 1), 95.0);
            context.Add(kyc);
        }
        else
        {
            kyc.Approve("Test User", "DOC123", new DateTime(1990, 1, 1), 95.0);
        }

        await context.SaveChangesAsync();
    }
}
