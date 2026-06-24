using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;

namespace Prueba.Infrastructure.Data;

public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly ICurrentTenant _currentTenant;
    private IDbContextTransaction? _currentTransaction;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Suppress PendingModelChangesWarning because we use dynamic tenant ID in query filters
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply schema per tenant
        if (!string.IsNullOrEmpty(_currentTenant.SchemaName))
        {
            modelBuilder.HasDefaultSchema(_currentTenant.SchemaName);
        }

        // Apply all IEntityTypeConfiguration<T> from loaded assemblies (modules)
        ApplyConfigurationsFromLoadedAssemblies(modelBuilder);

        // Apply global query filter for tenant isolation on all BaseEntity types
        var tenantId = _currentTenant.TenantId;
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var tenantIdProperty = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.TenantId));
                var tenantIdConstant = System.Linq.Expressions.Expression.Constant(tenantId.HasValue ? tenantId.Value : Guid.Empty);
                var body = System.Linq.Expressions.Expression.Equal(tenantIdProperty, tenantIdConstant);

                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(System.Linq.Expressions.Expression.Lambda(body, parameter));
            }
        }
    }

    private static void ApplyConfigurationsFromLoadedAssemblies(ModelBuilder modelBuilder)
    {
        var applyConfigurationsMethod = typeof(ModelBuilder)
            .GetMethod(nameof(ModelBuilder.ApplyConfigurationsFromAssembly));

        if (applyConfigurationsMethod is null) return;

        // Scan loaded assemblies that start with "Prueba.Modules"
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Prueba.Modules") == true);

        foreach (var assembly in moduleAssemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        _currentTransaction = await Database.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException("No active transaction.");

        await _currentTransaction.CommitAsync(cancellationToken);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException("No active transaction.");

        await _currentTransaction.RollbackAsync(cancellationToken);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    private void ApplyAuditInformation()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    if (entry.Entity.TenantId == Guid.Empty && _currentTenant.TenantId.HasValue)
                    {
                        entry.Entity.TenantId = _currentTenant.TenantId.Value;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
    }
}
