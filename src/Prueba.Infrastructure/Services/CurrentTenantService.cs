using Prueba.Application.Interfaces;

namespace Prueba.Infrastructure.Services;

public class CurrentTenantService : ICurrentTenant
{
    public Guid? TenantId { get; private set; }
    public string SchemaName { get; private set; } = "public";

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        TenantId = tenantId;
        SchemaName = $"tenant_{tenantId}";
    }

    public void SetSchema(string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        SchemaName = schemaName;
    }
}
