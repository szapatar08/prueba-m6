using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Prueba.Application.Interfaces;

namespace Prueba.Infrastructure.Services;

public class MinioStorageService : IObjectStorage
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IMinioClient minioClient, ILogger<MinioStorageService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        string bucketName,
        string objectName,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        // Ensure bucket exists
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
        var exists = await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);

        if (!exists)
        {
            var makeBucketArgs = new MakeBucketArgs().WithBucket(bucketName);
            await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
        }

        // Upload with server-side encryption
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType)
            .WithHeaders(new Dictionary<string, string>
            {
                ["x-amz-server-side-encryption"] = "AES256"
            });

        await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

        _logger.LogInformation("Uploaded object {ObjectName} to bucket {BucketName}", objectName, bucketName);

        return $"{bucketName}/{objectName}";
    }

    public async Task<Stream> DownloadAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();

        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(async (stream, ct) =>
            {
                await stream.CopyToAsync(memoryStream, ct);
                memoryStream.Position = 0;
            });

        await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);

        return memoryStream;
    }

    public async Task DeleteAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var removeObjectArgs = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);

        await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);

        _logger.LogInformation("Deleted object {ObjectName} from bucket {BucketName}", objectName, bucketName);
    }

    public async Task<bool> ExistsAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statObjectArgs, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
