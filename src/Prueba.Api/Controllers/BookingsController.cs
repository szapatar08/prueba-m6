using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Features.CancelBooking;
using Prueba.Modules.Booking.Features.CheckAvailability;
using Prueba.Modules.Booking.Features.CompleteBooking;
using Prueba.Modules.Booking.Features.ConfirmBooking;
using Prueba.Modules.Booking.Features.CreateBooking;
using Prueba.Modules.Booking.Features.GetPropertyBookings;
using Prueba.Modules.Booking.Features.ListBookings;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKycService _kycService;

    public BookingsController(
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

    /// <summary>
    /// Create a new booking (Guest only, with KYC gate)
    /// </summary>
    [Authorize(Roles = "Guest")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingCommand command, CancellationToken cancellationToken)
    {
        var guestId = GetCurrentUserId();

        var handler = new CreateBookingHandler(_repository, _currentTenant, _unitOfWork, _kycService);
        var result = await handler.Handle(command, guestId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("KYC"))
                return StatusCode(403, new { error = result.Error });
            if (result.Error.Contains("Dates unavailable"))
                return Conflict(new { error = result.Error });
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetMyBookings), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// Confirm a booking (Owner only)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPut("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var handler = new ConfirmBookingHandler(_repository, _currentTenant);
        var result = await handler.Handle(id, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("not found"))
                return NotFound(new { error = result.Error });
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Cancel a booking (Guest only, own bookings)
    /// </summary>
    [Authorize(Roles = "Guest")]
    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var guestId = GetCurrentUserId();
        var handler = new CancelBookingHandler(_repository, _currentTenant);
        var result = await handler.Handle(id, guestId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("not found"))
                return NotFound(new { error = result.Error });
            if (result.Error!.Contains("own bookings"))
                return Forbid();
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Complete a booking (Owner only)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPut("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var handler = new CompleteBookingHandler(_repository, _currentTenant);
        var result = await handler.Handle(id, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("not found"))
                return NotFound(new { error = result.Error });
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get current user's bookings (Guest only)
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMyBookings(CancellationToken cancellationToken)
    {
        var guestId = GetCurrentUserId();
        var handler = new ListBookingsHandler(_repository, _currentTenant);
        var result = await handler.Handle(guestId, cancellationToken);

        return Ok(result.Value);
    }

    /// <summary>
    /// Get bookings for a property (Owner only)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpGet("property/{propertyId:guid}")]
    public async Task<IActionResult> GetPropertyBookings(Guid propertyId, CancellationToken cancellationToken)
    {
        var handler = new GetPropertyBookingsHandler(_repository, _currentTenant);
        var result = await handler.Handle(propertyId, cancellationToken);

        return Ok(result.Value);
    }

    /// <summary>
    /// Check availability for a property (Public)
    /// </summary>
    [AllowAnonymous]
    [HttpGet("availability")]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] Guid propertyId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var handler = new CheckAvailabilityHandler(_repository, _currentTenant);
        var result = await handler.Handle(propertyId, startDate, endDate, cancellationToken);

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token");
        return Guid.Parse(userIdClaim);
    }
}
