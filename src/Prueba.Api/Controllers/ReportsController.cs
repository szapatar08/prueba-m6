using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba.Application.Interfaces;
using Prueba.Modules.Reports.Features.GenerateReport;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner")]
public class ReportsController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public ReportsController(
        IRepository repository,
        ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Generate Excel report for a specific property
    /// </summary>
    [HttpGet("property/{propertyId:guid}")]
    public async Task<IActionResult> GetPropertyReport(
        Guid propertyId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var query = new GenerateReportQuery(propertyId, startDate, endDate);
        var handler = new GenerateReportHandler(_repository, _currentTenant);
        var result = await handler.Handle(query, ownerId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return File(result.Value!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"property-report-{propertyId:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// Generate Excel report for the owner's entire portfolio
    /// </summary>
    [HttpGet("portfolio")]
    public async Task<IActionResult> GetPortfolioReport(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var query = new GenerateReportQuery(null, startDate, endDate);
        var handler = new GenerateReportHandler(_repository, _currentTenant);
        var result = await handler.Handle(query, ownerId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return File(result.Value!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"portfolio-report-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token");
        return Guid.Parse(userIdClaim);
    }
}
