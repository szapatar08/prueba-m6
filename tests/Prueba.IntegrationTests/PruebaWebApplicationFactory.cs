using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Modules.KYC.Entities;

namespace Prueba.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory that uses SQLite with a shared in-memory connection.
/// Provides a consistent tenant context and SQLite-compatible services for all tests.
/// </summary>
public class PruebaWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly Guid _defaultTenantId = Guid.NewGuid();
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Create a single shared SQLite connection that stays open for the lifetime of the test
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor is not null)
                services.Remove(dbContextDescriptor);

            // Remove existing ICurrentTenant registration
            var tenantDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ICurrentTenant));
            if (tenantDescriptor is not null)
                services.Remove(tenantDescriptor);

            // Remove existing IRepository registration
            var repoDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRepository));
            if (repoDescriptor is not null)
                services.Remove(repoDescriptor);

            // Remove existing IKycService registration
            var kycDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IKycService));
            if (kycDescriptor is not null)
                services.Remove(kycDescriptor);

            // Register SQLite with the shared connection
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            // Register a test-friendly tenant service
            services.AddScoped<ICurrentTenant>(sp => new TestCurrentTenantService(_defaultTenantId));

            // Register SQLite-compatible repository
            services.AddScoped<IRepository, SqliteCompatibleRepository>();

            // Register SQLite-compatible KYC service
            services.AddScoped<IKycService, SqliteCompatibleKycService>();

            // Register test stubs for services not available in testing
            services.AddScoped<IObjectStorage, NoOpObjectStorage>();
            services.AddScoped<IEmailService, NoOpEmailService>();
            services.AddScoped<Hangfire.IBackgroundJobClient, NoOpBackgroundJobClient>();
        });
    }

    public Guid DefaultTenantId => _defaultTenantId;

    public async Task InitializeAsync()
    {
        ForceLoadModuleAssemblies();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            _connection.Close();
            _connection.Dispose();
        }
        await Task.CompletedTask;
    }

    private static void ForceLoadModuleAssemblies()
    {
        _ = typeof(Prueba.Modules.Identity.Entities.User).Assembly;
        _ = typeof(Prueba.Modules.Properties.Entities.Property).Assembly;
        _ = typeof(Prueba.Modules.Booking.Entities.BookingEntity).Assembly;
        _ = typeof(Prueba.Modules.Wishlist.Entities.WishlistItem).Assembly;
        _ = typeof(Prueba.Modules.KYC.Entities.KycValidation).Assembly;
        _ = typeof(Prueba.Modules.Notifications.Entities.Notification).Assembly;
        _ = typeof(Prueba.Modules.Dashboard.Features.GetOccupancyRate.GetOccupancyRateQuery).Assembly;
        _ = typeof(Prueba.Modules.Reports.Features.GenerateReport.GenerateReportQuery).Assembly;
    }
}

/// <summary>
/// Test tenant service that always uses "public" schema for SQLite compatibility.
/// </summary>
public class TestCurrentTenantService : ICurrentTenant
{
    public TestCurrentTenantService(Guid tenantId)
    {
        TenantId = tenantId;
        SchemaName = "public";
    }

    public Guid? TenantId { get; private set; }
    public string SchemaName { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        TenantId = tenantId;
        SchemaName = "public";
    }

    public void SetSchema(string schemaName)
    {
        SchemaName = "public";
    }
}

/// <summary>
/// SQLite-compatible repository that handles PostgreSQL-style raw SQL.
/// </summary>
public class SqliteCompatibleRepository : IRepository
{
    private readonly AppDbContext _context;

    public SqliteCompatibleRepository(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<T> Query<T>() where T : class => _context.Set<T>().AsQueryable();
    public void Add<T>(T entity) where T : class => _context.Set<T>().Add(entity);
    public void AddRange<T>(IEnumerable<T> entities) where T : class => _context.Set<T>().AddRange(entities);
    public void Remove<T>(T entity) where T : class => _context.Set<T>().Remove(entity);
    public async Task<T?> GetByIdAsync<T>(Guid id, CancellationToken ct = default) where T : class
        => await _context.Set<T>().FindAsync(new object[] { id }, ct);
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task<int> ExecuteSqlRawAsync(string sql, CancellationToken ct = default, params object[] parameters)
    {
        // Detect booking overlap query and use LINQ
        if (sql.Contains("\"Bookings\"") && sql.Contains("\"Status\" = 'Confirmed'"))
        {
            var propertyId = (Guid)parameters[0];
            var tenantId = (Guid)parameters[1];
            var startDate = (DateOnly)parameters[2];
            var endDate = (DateOnly)parameters[3];

            return await _context.Set<Prueba.Modules.Booking.Entities.BookingEntity>()
                .IgnoreQueryFilters()
                .Where(b => b.PropertyId == propertyId
                    && b.TenantId == tenantId
                    && b.Status == Prueba.Modules.Booking.Entities.BookingStatus.Confirmed
                    && b.StartDate < endDate
                    && b.EndDate > startDate)
                .CountAsync(ct);
        }

        // For other queries, strip PostgreSQL quoting
        var cleanSql = sql.Replace("\"", "");
        return await _context.Database.ExecuteSqlRawAsync(cleanSql, parameters, ct);
    }
}

/// <summary>
/// SQLite-compatible KYC service (replaces PostgreSQL raw SQL with LINQ).
/// </summary>
public class SqliteCompatibleKycService : IKycService
{
    private readonly AppDbContext _context;

    public SqliteCompatibleKycService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasApprovedKycAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<KycValidation>()
            .IgnoreQueryFilters()
            .AnyAsync(k => k.UserId == userId && k.Status == KycStatus.Approved, cancellationToken);
    }
}

/// <summary>
/// No-op object storage stub for integration tests.
/// </summary>
public class NoOpObjectStorage : IObjectStorage
{
    public Task<string> UploadAsync(string bucketName, string objectName, Stream data, string contentType, CancellationToken cancellationToken = default)
        => Task.FromResult($"test://{bucketName}/{objectName}");
    public Task<Stream> DownloadAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(new MemoryStream());
    public Task DeleteAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task<bool> ExistsAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

/// <summary>
/// No-op email service stub for integration tests.
/// </summary>
public class NoOpEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// No-op Hangfire background job client stub for integration tests.
/// </summary>
public class NoOpBackgroundJobClient : Hangfire.IBackgroundJobClient
{
    public Hangfire.Common.Job? Current => null;
    public string Enqueue(Hangfire.Common.Job job, string methodName) => Guid.NewGuid().ToString();
    public string Enqueue<T>(System.Linq.Expressions.Expression<Action<T>> methodCall) => Guid.NewGuid().ToString();
    public bool Requeue(string jobId, string fromState) => true;
    public bool Requeue(string jobId) => true;
    public bool ChangeState(string jobId, Hangfire.States.IState state, string expectedState) => true;
    public bool ChangeState(string jobId, Hangfire.States.IState state) => true;
    public string Create(Hangfire.Common.Job job, Hangfire.States.IState state) => Guid.NewGuid().ToString();
}
