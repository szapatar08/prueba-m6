namespace Prueba.Application.Interfaces;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
    string SchemaName { get; }
    void SetTenant(Guid tenantId);
    void SetSchema(string schemaName);
}
