using System.Data;
using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Booking.Features.CreateBooking;

public class CreateBookingHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKycService _kycService;

    public CreateBookingHandler(
        IRepository repository,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork,
        IKycService kycService)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
        _kycService = kycService;
    }

    public async Task<Result<BookingResponse>> Handle(
        CreateBookingCommand command,
        Guid guestId,
        CancellationToken cancellationToken)
    {
        // Validate
        var validator = new CreateBookingValidator();
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<BookingResponse>.Fail(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        // KYC gate: first booking requires KYC approval
        var hasKyc = await _kycService.HasApprovedKycAsync(guestId, cancellationToken);
        if (!hasKyc)
        {
            var hasPreviousBookings = await _repository.Query<BookingEntity>()
                .IgnoreQueryFilters()
                .AnyAsync(b => b.GuestId == guestId, cancellationToken);

            if (!hasPreviousBookings)
            {
                return Result<BookingResponse>.Fail(
                    "KYC verification required for first booking. Please complete identity verification.");
            }
        }

        var tenantId = _currentTenant.TenantId!.Value;

        // Begin SERIALIZABLE transaction for atomic overlap check + insert
        // SERIALIZABLE is the highest isolation level — prevents phantom reads,
        // ensuring no other transaction can insert a conflicting booking between our check and insert.
        await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            // Overlap check: StartDate < ExistingEndDate AND EndDate > ExistingStartDate
            // This catches: same dates, partial overlap, full overlap
            // Adjacent bookings (EndDate == ExistingStartDate) are NOT overlaps
            var hasConflict = await CheckForOverlapAsync(
                command.PropertyId, tenantId, command.StartDate, command.EndDate, cancellationToken);

            if (hasConflict)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result<BookingResponse>.Fail("Dates unavailable. The selected dates overlap with an existing booking.");
            }

            var booking = BookingEntity.Create(
                propertyId: command.PropertyId,
                guestId: guestId,
                startDate: command.StartDate,
                endDate: command.EndDate,
                totalPrice: command.TotalPrice,
                tenantId: tenantId);

            _repository.Add(booking);
            await _repository.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result<BookingResponse>.Success(new BookingResponse(
                booking.Id,
                booking.PropertyId,
                booking.GuestId,
                booking.StartDate,
                booking.EndDate,
                booking.Status,
                booking.TotalPrice,
                booking.CheckInTime,
                booking.CheckOutTime,
                booking.CreatedAt));
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Checks for overlapping bookings. Uses raw SQL in production (PostgreSQL) for reliability.
    /// Virtual to allow test overrides (SQLite doesn't support PostgreSQL-style quoted identifiers).
    /// </summary>
    protected virtual async Task<bool> CheckForOverlapAsync(
        Guid propertyId,
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        // Raw SQL overlap check — reliable across providers
        // Formula: StartDate < ExistingEndDate AND EndDate > ExistingStartDate
        var conflictCount = await _repository.ExecuteSqlRawAsync(
            """
            SELECT COUNT(*) FROM "Bookings"
            WHERE "PropertyId" = {0}
              AND "TenantId" = {1}
              AND "Status" = 'Confirmed'
              AND "StartDate" < {3}
              AND "EndDate" > {2}
            """,
            cancellationToken,
            propertyId,
            tenantId,
            startDate,
            endDate);

        return conflictCount > 0;
    }
}

public record BookingResponse(
    Guid Id,
    Guid PropertyId,
    Guid GuestId,
    DateOnly StartDate,
    DateOnly EndDate,
    BookingStatus Status,
    decimal TotalPrice,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    DateTime CreatedAt);
