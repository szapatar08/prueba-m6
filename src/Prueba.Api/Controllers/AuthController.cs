using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba.Application.Interfaces;
using Prueba.Modules.Identity.Features.Login;
using Prueba.Modules.Identity.Features.Register;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthController(
        IRepository repository,
        ICurrentTenant currentTenant,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        var handler = new RegisterHandler(_repository, _currentTenant);
        var result = await handler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
            return Conflict(new { error = result.Error });

        return CreatedAtAction(nameof(Register), result.Value);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var handler = new LoginHandler(_repository, _currentTenant, _jwtTokenGenerator);
        var result = await handler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
            return Unauthorized(new { error = result.Error });

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var tenantId = User.FindFirst("tenant_id")?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new
        {
            userId,
            email,
            tenantId,
            roles
        });
    }
}
