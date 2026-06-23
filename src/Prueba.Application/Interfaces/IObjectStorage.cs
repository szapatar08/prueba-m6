namespace Prueba.Application.Interfaces;

public interface IObjectStorage
{
    Task<string> UploadAsync(
        string bucketName,
        string objectName,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default);
}
