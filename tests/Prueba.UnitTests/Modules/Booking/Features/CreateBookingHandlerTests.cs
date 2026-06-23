using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Booking.Features.CreateBooking;

namespace Prueba.UnitTests.Modules.Booking.Features;

/// <summary>
/// Test subclass that overrides the raw SQL overlap check with a LINQ-based version
/// for SQLite compatibility. Production uses raw SQL (PostgreSQL).
/// </summary>
public class TestableCreateBookingHandler : CreateBookingHandler
{
    private readonly IRepository _repository;

    public TestableCreateBookingHandler(
        IRepository repository,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork,
        IKycService kycService)
        : base(repository, currentTenant, unitOfWork, kycService)
    {
        _repository = repository;
    }

    protected override async Task<bool> CheckForOverlapAsync(
        Guid propertyId,
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        return await _repository.Query<BookingEntity>()
            .IgnoreQueryFilters()
            .Where(b => b.PropertyId == propertyId
                && b.TenantId == tenantId
                && b.Status == BookingStatus.Confirmed
                && b.StartDate < endDate
                && b.EndDate > startDate)
            .AnyAsync(cancellationToken);
    }
}

public class CreateBookingHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IKycService> _kycServiceMock;
    private readonly TestableCreateBookingHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _guestId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public CreateBookingHandlerTests()
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
        _kycServiceMock.Setup(x => x.HasApprovedKycAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _context = new AppDbContext(options, _currentTenantMock.Object);
        _context.Database.EnsureCreated();
        _repository = new Repository(_context);
        _handler = new TestableCreateBookingHandler(_repository, _currentTenantMock.Object, _context, _kycServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateBooking()
    {
        // Arrange
        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 5),
            TotalPrice: 600.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.PropertyId.Should().Be(_propertyId);
        result.Value.GuestId.Should().Be(_guestId);
        result.Value.Status.Should().Be(BookingStatus.Pending);
        result.Value.TotalPrice.Should().Be(600.00m);
    }

    [Fact]
    public async Task Handle_ShouldPersistBookingToDatabase()
    {
        // Arrange
        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 5),
            TotalPrice: 600.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        var saved = await _context.Set<BookingEntity>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == result.Value!.Id);
        saved.Should().NotBeNull();
        saved!.PropertyId.Should().Be(_propertyId);
        saved.GuestId.Should().Be(_guestId);
        saved.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Handle_WithNoConflictingBookings_ShouldSucceed()
    {
        // Arrange - existing booking Aug 10-15, new booking Aug 1-5 (no overlap)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 5),
            TotalPrice: 400.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithOverlappingConfirmedBooking_ShouldReject()
    {
        // Arrange - existing confirmed booking Aug 10-15, new booking Aug 12-18 (overlap)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 12),
            EndDate: new DateOnly(2026, 8, 18),
            TotalPrice: 600.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Dates unavailable");
    }

    [Fact]
    public async Task Handle_WithOverlappingPendingBooking_ShouldNotReject()
    {
        // Arrange - pending bookings should NOT block (only confirmed bookings block)
        var pendingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        // Don't confirm - stays Pending
        _repository.Add(pendingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 12),
            EndDate: new DateOnly(2026, 8, 18),
            TotalPrice: 600.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithCancelledBookingOverlap_ShouldNotReject()
    {
        // Arrange - cancelled bookings should NOT block
        var cancelledBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        cancelledBooking.Confirm();
        cancelledBooking.Cancel();
        _repository.Add(cancelledBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 12),
            EndDate: new DateOnly(2026, 8, 18),
            TotalPrice: 600.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AdjacentBooking_ShouldSucceed()
    {
        // Arrange - existing booking Aug 10-15, new booking Aug 15-20 (adjacent, not overlapping)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 15),
            EndDate: new DateOnly(2026, 8, 20),
            TotalPrice: 500.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OverlapCheckIsPerProperty_ShouldNotBlockDifferentProperty()
    {
        // Arrange - booking on different property with same dates
        var differentPropertyId = Guid.NewGuid();
        var existingBooking = BookingEntity.Create(
            differentPropertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId, // Different property
            StartDate: new DateOnly(2026, 8, 10),
            EndDate: new DateOnly(2026, 8, 15),
            TotalPrice: 500.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NewBookingEndingOnExistingStart_ShouldSucceed()
    {
        // Arrange - existing booking Aug 10-15, new booking Aug 5-10 (no overlap: EndDate == ExistingStartDate)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 5),
            EndDate: new DateOnly(2026, 8, 10),
            TotalPrice: 500.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NewBookingStartingOnExistingEnd_ShouldSucceed()
    {
        // Arrange - existing booking Aug 10-15, new booking Aug 15-20 (no overlap: StartDate == ExistingEndDate)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 15),
            EndDate: new DateOnly(2026, 8, 20),
            TotalPrice: 500.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // === Overlap Formula Tests ===

    [Fact]
    public async Task Handle_SameDates_ShouldReject()
    {
        // Arrange - existing confirmed booking Aug 10-15, new booking Aug 10-15 (exact same dates)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 10),
            EndDate: new DateOnly(2026, 8, 15),
            TotalPrice: 500.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Dates unavailable");
    }

    [Fact]
    public async Task Handle_PartialOverlapStart_ShouldReject()
    {
        // Arrange - existing confirmed booking Aug 10-15, new booking Aug 8-12 (partial overlap at start)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 8),
            EndDate: new DateOnly(2026, 8, 12),
            TotalPrice: 400.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Dates unavailable");
    }

    [Fact]
    public async Task Handle_PartialOverlapEnd_ShouldReject()
    {
        // Arrange - existing confirmed booking Aug 10-15, new booking Aug 13-18 (partial overlap at end)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 13),
            EndDate: new DateOnly(2026, 8, 18),
            TotalPrice: 500.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Dates unavailable");
    }

    [Fact]
    public async Task Handle_FullOverlap_ShouldReject()
    {
        // Arrange - existing confirmed booking Aug 10-15, new booking Aug 5-20 (fully contains existing)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 5),
            EndDate: new DateOnly(2026, 8, 20),
            TotalPrice: 1500.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Dates unavailable");
    }

    [Fact]
    public async Task Handle_ExistingFullyContainsNew_ShouldReject()
    {
        // Arrange - existing confirmed booking Aug 5-20, new booking Aug 10-15 (existing fully contains new)
        var existingBooking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 20), 1500m, _tenantId);
        existingBooking.Confirm();
        _repository.Add(existingBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 10),
            EndDate: new DateOnly(2026, 8, 15),
            TotalPrice: 500.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Dates unavailable");
    }

    // === KYC Gate Tests ===

    [Fact]
    public async Task Handle_WithKycNotApprovedAndNoPreviousBookings_ShouldReject()
    {
        // Arrange - KYC not approved, no previous bookings
        _kycServiceMock.Setup(x => x.HasApprovedKycAsync(_guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateBookingCommand(
            PropertyId: _propertyId,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 5),
            TotalPrice: 600.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("KYC");
    }

    [Fact]
    public async Task Handle_WithKycNotApprovedButHasPreviousBookings_ShouldSucceed()
    {
        // Arrange - KYC not approved, but guest has previous bookings
        _kycServiceMock.Setup(x => x.HasApprovedKycAsync(_guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var previousBooking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5), 400m, _tenantId);
        _repository.Add(previousBooking);
        await _repository.SaveChangesAsync();

        var command = new CreateBookingCommand(
            PropertyId: Guid.NewGuid(),
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 5),
            TotalPrice: 600.00m);

        // Act
        var result = await _handler.Handle(command, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
