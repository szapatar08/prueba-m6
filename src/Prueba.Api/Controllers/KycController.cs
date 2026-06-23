using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba.Application.Interfaces;
using Prueba.Modules.KYC.Features.GetKycStatus;
using Prueba.Modules.KYC.Features.IsKycApproved;
using Prueba.Modules.KYC.Features.UploadKycDocument;
using Prueba.Modules.KYC.Jobs;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KycController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IObjectStorage _objectStorage;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public KycController(
        IRepository repository,
        ICurrentTenant currentTenant,
        IObjectStorage objectStorage,
        IBackgroundJobClient backgroundJobClient)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _objectStorage = objectStorage;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// Upload identity document for KYC verification
    /// </summary>
    [Authorize]
    [HttpPost("documents")]
    public async Task<IActionResult> UploadDocument(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var userId = GetCurrentUserId();
        var handler = new UploadKycDocumentHandler(_repository, _currentTenant, _objectStorage);

        using var stream = file.OpenReadStream();
        var command = new UploadKycDocumentCommand(
            file.FileName,
            file.ContentType,
            stream);

        var result = await handler.Handle(command, userId, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        // Enqueue background job for OCR processing
        _backgroundJobClient.Enqueue<ProcessKycDocumentJob>(
            job => job.ExecuteAsync(result.Value!.ValidationId, CancellationToken.None));

        return CreatedAtAction(nameof(GetStatus), new { }, result.Value);
    }

    /// <summary>
    /// Get the authenticated user's KYC status
    /// </summary>
    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var handler = new GetKycStatusHandler(_repository, _currentTenant);
        var result = await handler.Handle(userId, cancellationToken);

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token");
        return Guid.Parse(userIdClaim);
    }
}
