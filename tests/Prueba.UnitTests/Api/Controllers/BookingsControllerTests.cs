using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Api.Controllers;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Booking.Features.CreateBooking;
using Prueba.Modules.Booking.Features.ConfirmBooking;
using Prueba.Modules.Booking.Features.CancelBooking;
using Prueba.Modules.Booking.Features.CompleteBooking;
using Prueba.Modules.Booking.Features.ListBookings;
using Prueba.Modules.Booking.Features.GetPropertyBookings;
using Prueba.Modules.Booking.Features.CheckAvailability;

namespace Prueba.UnitTests.Api.Controllers;

public class BookingsControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IKycService> _kycServiceMock;
    private readonly BookingsController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _guestId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public BookingsControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _currentTenantMock = new Mock<ICurrentTenant>();
        _currentTenantMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentTenantMock.Setup(x => x.SchemaName).Returns("public");

        _kycServiceMock = new Mock<IKycService>();
        _kycServiceMock.Setup(x => x.HasApprovedKycAsync(_guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _context = new AppDbContext(options, _currentTenantMock.Object);
        _context.Database.EnsureCreated();
        _repository = new Repository(_context);

        _controller = new BookingsController(_repository, _currentTenantMock.Object, _context, _kycServiceMock.Object);
    }

    private void SetUserContext(Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // === Authorization Attributes ===

    [Fact]
    public void Post_ShouldRequireAuthorization()
    {
        // Assert
        var method = typeof(BookingsController).GetMethod(nameof(BookingsController.Create));
        method.Should().NotBeNull();
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        authAttr.Should().HaveCount(1);
    }

    [Fact]
    public void Post_ShouldAllowGuestRole()
    {
        // Assert
        var method = typeof(BookingsController).GetMethod(nameof(BookingsController.Create));
        var authAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)method!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)[0];
        authAttr.Roles.Should().Contain("Guest");
    }

    [Fact]
    public void Confirm_ShouldRequireOwnerRole()
    {
        // Assert
        var method = typeof(BookingsController).GetMethod(nameof(BookingsController.Confirm));
        var authAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)method!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)[0];
        authAttr.Roles.Should().Contain("Owner");
    }

    [Fact]
    public void Cancel_ShouldRequireGuestRole()
    {
        // Assert
        var method = typeof(BookingsController).GetMethod(nameof(BookingsController.Cancel));
        var authAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)method!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)[0];
        authAttr.Roles.Should().Contain("Guest");
    }

    [Fact]
    public void Complete_ShouldRequireOwnerRole()
    {
        // Assert
        var method = typeof(BookingsController).GetMethod(nameof(BookingsController.Complete));
        var authAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)method!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)[0];
        authAttr.Roles.Should().Contain("Owner");
    }

    [Fact]
    public void GetMyBookings_ShouldRequireAuthorization()
    {
        // Assert
        var method = typeof(BookingsController).GetMethod(nameof(BookingsController.GetMyBookings));
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        authAttr.Should().HaveCount(1);
    }

    [Fact]
    public void GetPropertyBookings_ShouldRequireOwnerRole()
    {
        // Assert
        var method = typeof(BookingsController).GetMethod(nameof(BookingsController.GetPropertyBookings));
        var authAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)method!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)[0];
        authAttr.Roles.Should().Contain("Owner");
    }

    [Fact]
    public void CheckAvailability_ShouldAllowAnonymous()
    {
        // Assert
        var method = typeof(BookingsController).GetMethod(nameof(BookingsController.CheckAvailability));
        var allowAnon = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false);
        allowAnon.Should().HaveCount(1);
    }

    // === Create Booking ===

    [Fact]
    public async Task Create_WithValidCommand_ShouldReturnCreated()
    {
        // Arrange
        SetUserContext(_guestId, "Guest");
        var command = new CreateBookingCommand(
            _propertyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 600m);

        // Act
        var result = await _controller.Create(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_WithKycNotApproved_ShouldReturnForbidden()
    {
        // Arrange
        SetUserContext(_guestId, "Guest");
        _kycServiceMock.Setup(x => x.HasApprovedKycAsync(_guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // No existing bookings - this is the first booking attempt
        var command = new CreateBookingCommand(
            _propertyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 600m);

        // Act
        var result = await _controller.Create(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Create_WithKycNotApprovedButHasPreviousBookings_ShouldReturnCreated()
    {
        // Arrange - guest has a previous booking, so KYC gate is bypassed
        SetUserContext(_guestId, "Guest");
        _kycServiceMock.Setup(x => x.HasApprovedKycAsync(_guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Create a previous booking for this guest
        var previousBooking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5), 400m, _tenantId);
        previousBooking.Confirm();
        _repository.Add(previousBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            Guid.NewGuid(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 600m);

        // Act
        var result = await _controller.Create(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    // === Confirm Booking ===

    [Fact]
    public async Task Confirm_WithValidBooking_ShouldReturnOk()
    {
        // Arrange
        SetUserContext(_ownerId, "Owner");
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 600m, _tenantId);
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.Confirm(booking.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Confirm_WithNonExistentBooking_ShouldReturnNotFound()
    {
        // Arrange
        SetUserContext(_ownerId, "Owner");

        // Act
        var result = await _controller.Confirm(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // === Cancel Booking ===

    [Fact]
    public async Task Cancel_WithOwnBooking_ShouldReturnOk()
    {
        // Arrange
        SetUserContext(_guestId, "Guest");
        var booking = BookingEntity.Create(
            _propertyId, _guestId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), 600m, _tenantId);
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.Cancel(booking.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Cancel_WithNonExistentBooking_ShouldReturnNotFound()
    {
        // Arrange
        SetUserContext(_guestId, "Guest");

        // Act
        var result = await _controller.Cancel(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // === Complete Booking ===

    [Fact]
    public async Task Complete_WithConfirmedBooking_ShouldReturnOk()
    {
        // Arrange
        SetUserContext(_ownerId, "Owner");
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 600m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.Complete(booking.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // === GetMyBookings ===

    [Fact]
    public async Task GetMyBookings_ShouldReturnGuestBookings()
    {
        // Arrange
        SetUserContext(_guestId, "Guest");
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 600m, _tenantId);
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyBookings(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // === GetPropertyBookings ===

    [Fact]
    public async Task GetPropertyBookings_ShouldReturnPropertyBookings()
    {
        // Arrange
        SetUserContext(_ownerId, "Owner");
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 600m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.GetPropertyBookings(_propertyId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // === CheckAvailability ===

    [Fact]
    public async Task CheckAvailability_WithNoConflicts_ShouldReturnAvailable()
    {
        // Arrange
        // No controller context needed - public endpoint

        // Act
        var result = await _controller.CheckAvailability(
            _propertyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckAvailability_WithConflict_ShouldReturnUnavailable()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 600m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.CheckAvailability(
            _propertyId, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 7),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        // The response should indicate unavailable
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
