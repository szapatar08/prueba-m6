using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Features.BrowseCatalog;
using Prueba.Modules.Properties.Features.CreateProperty;
using Prueba.Modules.Properties.Features.DeleteProperty;
using Prueba.Modules.Properties.Features.GetProperty;
using Prueba.Modules.Properties.Features.ManageAvailability;
using Prueba.Modules.Properties.Features.UpdateProperty;
using Prueba.Modules.Properties.Features.UploadImages;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public PropertiesController(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Create a new property (Owner only)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePropertyCommand command, CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var handler = new CreatePropertyHandler(_repository, _currentTenant);
        var result = await handler.Handle(command, ownerId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// Update an existing property (Owner only, must own the property)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePropertyCommand command, CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var handler = new UpdatePropertyHandler(_repository, _currentTenant);
        var result = await handler.Handle(command, id, ownerId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("not found"))
                return NotFound(new { error = result.Error });
            if (result.Error.Contains("own properties"))
                return Forbid();
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Delete a property (Owner only, must own the property)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var handler = new DeletePropertyHandler(_repository, _currentTenant);
        var result = await handler.Handle(id, ownerId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("not found"))
                return NotFound(new { error = result.Error });
            if (result.Error.Contains("own properties"))
                return Forbid();
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Get property details (Public)
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var handler = new GetPropertyHandler(_repository);
        var result = await handler.Handle(id, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Browse property catalog with filters (Public)
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Browse(
        [FromQuery] string? city,
        [FromQuery] string? country,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int? minGuests,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new BrowseCatalogQuery(
            City: city,
            Country: country,
            StartDate: startDate,
            EndDate: endDate,
            MinGuests: minGuests,
            Page: page,
            PageSize: pageSize);

        var handler = new BrowseCatalogHandler(_repository);
        var result = await handler.Handle(query, cancellationToken);

        return Ok(result.Value);
    }

    /// <summary>
    /// Set availability for a single date (Owner only)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPost("{id:guid}/availability")]
    public async Task<IActionResult> SetAvailability(
        Guid id,
        [FromBody] SetAvailabilityCommand command,
        CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var handler = new ManageAvailabilityHandler(_repository, _currentTenant);
        var result = await handler.SetAvailability(command, id, ownerId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("not found"))
                return NotFound(new { error = result.Error });
            if (result.Error.Contains("own properties"))
                return Forbid();
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Set availability for a date range (Owner only)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPost("{id:guid}/availability/range")]
    public async Task<IActionResult> SetAvailabilityRange(
        Guid id,
        [FromBody] SetAvailabilityRangeCommand command,
        CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var handler = new ManageAvailabilityHandler(_repository, _currentTenant);
        var result = await handler.SetAvailabilityRange(command, id, ownerId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("not found"))
                return NotFound(new { error = result.Error });
            if (result.Error.Contains("own properties"))
                return Forbid();
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Upload images for a property (Owner only)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPost("{id:guid}/images")]
    public async Task<IActionResult> UploadImages(
        Guid id,
        [FromBody] List<UploadImageRequest> images,
        CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        var handler = new UploadImagesHandler(_repository, _currentTenant);
        var result = await handler.Handle(id, ownerId, images, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("not found"))
                return NotFound(new { error = result.Error });
            if (result.Error.Contains("own properties"))
                return Forbid();
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token");
        return Guid.Parse(userIdClaim);
    }
}
