using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba.Application.Interfaces;
using Prueba.Modules.Wishlist.Features.AddToWishlist;
using Prueba.Modules.Wishlist.Features.GetWishlist;
using Prueba.Modules.Wishlist.Features.IsInWishlist;
using Prueba.Modules.Wishlist.Features.RemoveFromWishlist;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WishlistController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public WishlistController(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Add a property to the authenticated user's wishlist (idempotent)
    /// </summary>
    [Authorize]
    [HttpPost("{propertyId:guid}")]
    public async Task<IActionResult> AddToWishlist(Guid propertyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var handler = new AddToWishlistHandler(_repository, _currentTenant);
        var result = await handler.Handle(new AddToWishlistCommand(propertyId), userId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Remove a property from the authenticated user's wishlist (idempotent)
    /// </summary>
    [Authorize]
    [HttpDelete("{propertyId:guid}")]
    public async Task<IActionResult> RemoveFromWishlist(Guid propertyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var handler = new RemoveFromWishlistHandler(_repository, _currentTenant);
        var result = await handler.Handle(new RemoveFromWishlistCommand(propertyId), userId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    /// <summary>
    /// Get the authenticated user's wishlist with property details
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetWishlist(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var handler = new GetWishlistHandler(_repository, _currentTenant);
        var result = await handler.Handle(userId, cancellationToken);

        return Ok(result.Value);
    }

    /// <summary>
    /// Check if a property is in the authenticated user's wishlist
    /// </summary>
    [Authorize]
    [HttpGet("{propertyId:guid}/check")]
    public async Task<IActionResult> IsInWishlist(Guid propertyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var handler = new IsInWishlistHandler(_repository, _currentTenant);
        var result = await handler.HandleAsync(propertyId, userId, cancellationToken);

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token");
        return Guid.Parse(userIdClaim);
    }
}
