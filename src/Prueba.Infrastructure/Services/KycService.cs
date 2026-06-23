using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;

namespace Prueba.Infrastructure.Services;

public class KycService : IKycService
{
    private readonly AppDbContext _context;

    public KycService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasApprovedKycAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Check KycValidation table for approved status
        // Uses raw SQL to avoid module dependency
        var count = await _context.Database.ExecuteSqlRawAsync(
            """
            SELECT COUNT(*) FROM "KycValidations"
            WHERE "UserId" = {0} AND "Status" = 'Approved'
            """,
            cancellationToken,
            userId);

        return count > 0;
    }
}
