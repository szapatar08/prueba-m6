namespace Prueba.Application.Interfaces;

public interface IRepository
{
    IQueryable<T> Query<T>() where T : class;
    void Add<T>(T entity) where T : class;
    void AddRange<T>(IEnumerable<T> entities) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken = default, params object[] parameters);
}
