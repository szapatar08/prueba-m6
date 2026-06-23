namespace Prueba.Application.Interfaces;

public interface IKycService
{
    Task<bool> HasApprovedKycAsync(Guid userId, CancellationToken cancellationToken = default);
}
