using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;

namespace Prueba.Infrastructure.Services;

public class Repository : IRepository
{
    private readonly AppDbContext _context;

    public Repository(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<T> Query<T>() where T : class
    {
        return _context.Set<T>().AsQueryable();
    }

    public void Add<T>(T entity) where T : class
    {
        _context.Set<T>().Add(entity);
    }

    public void AddRange<T>(IEnumerable<T> entities) where T : class
    {
        _context.Set<T>().AddRange(entities);
    }

    public void Remove<T>(T entity) where T : class
    {
        _context.Set<T>().Remove(entity);
    }

    public async Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class
    {
        return await _context.Set<T>().FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken = default, params object[] parameters)
    {
        return await _context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }
}
