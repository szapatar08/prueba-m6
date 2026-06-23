using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba.Application.Interfaces;
using Prueba.Modules.Dashboard.Features.GetBookingTrends;
using Prueba.Modules.Dashboard.Features.GetOccupancyRate;
using Prueba.Modules.Dashboard.Features.GetRevenue;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner")]
public class DashboardController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public DashboardController(
        IRepository repository,
        ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Get occupancy rate for a property in a given period
    /// </summary>
    [HttpGet("occupancy")]
    public async Task<IActionResult> GetOccupancyRate(
        [FromQuery] Guid propertyId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var query = new GetOccupancyRateQuery(propertyId, startDate, endDate);
        var handler = new GetOccupancyRateHandler(_repository, _currentTenant);
        var result = await handler.Handle(query, ownerId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get revenue for a property in a given period
    /// </summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] Guid propertyId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var query = new GetRevenueQuery(propertyId, startDate, endDate);
        var handler = new GetRevenueHandler(_repository, _currentTenant);
        var result = await handler.Handle(query, ownerId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get booking trends for a property (monthly or weekly)
    /// </summary>
    [HttpGet("trends")]
    public async Task<IActionResult> GetBookingTrends(
        [FromQuery] Guid propertyId,
        [FromQuery] string period,
        CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();

        if (!Enum.TryParse<TrendPeriod>(period, true, out var trendPeriod))
            return BadRequest(new { error = "Invalid period. Use 'monthly' or 'weekly'." });

        var query = new GetBookingTrendsQuery(propertyId, trendPeriod);
        var handler = new GetBookingTrendsHandler(_repository, _currentTenant);
        var result = await handler.Handle(query, ownerId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token");
        return Guid.Parse(userIdClaim);
    }
}
