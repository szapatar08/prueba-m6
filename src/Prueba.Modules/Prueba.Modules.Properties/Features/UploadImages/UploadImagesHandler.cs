using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Properties.Features.UploadImages;

public class UploadImagesHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public UploadImagesHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<List<ImageResponse>>> Handle(
        Guid propertyId,
        Guid ownerId,
        List<UploadImageRequest> images,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var property = await _repository.Query<Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.TenantId == tenantId, cancellationToken);

        if (property is null)
            return Result<List<ImageResponse>>.Fail("Property not found");

        if (property.OwnerId != ownerId)
            return Result<List<ImageResponse>>.Fail("You can only upload images for your own properties");

        if (images.Count == 0)
            return Result<List<ImageResponse>>.Fail("At least one image is required");

        var responses = new List<ImageResponse>();

        foreach (var imageRequest in images)
        {
            var image = PropertyImage.Create(
                propertyId: propertyId,
                url: imageRequest.Url,
                isPrimary: imageRequest.IsPrimary,
                tenantId: tenantId);

            _repository.Add(image);
            responses.Add(new ImageResponse(image.Id, image.Url, image.IsPrimary));
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<List<ImageResponse>>.Success(responses);
    }
}

public record UploadImageRequest(string Url, bool IsPrimary);

public record ImageResponse(Guid Id, string Url, bool IsPrimary);
