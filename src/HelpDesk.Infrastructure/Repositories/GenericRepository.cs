using HelpDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Repositories;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    IQueryable<T> Query();
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveAsync(CancellationToken ct = default);
}

/// <summary>EF Core DbContext already provides Unit-of-Work + Repository semantics; this wraps it explicitly.</summary>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly HelpDeskDbContext Db;
    public GenericRepository(HelpDeskDbContext db) => Db = db;

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Db.Set<T>().FindAsync([id], ct).AsTask();
    public IQueryable<T> Query() => Db.Set<T>();
    public async Task AddAsync(T entity, CancellationToken ct = default) => await Db.Set<T>().AddAsync(entity, ct);
    public void Update(T entity) => Db.Set<T>().Update(entity);
    public void Remove(T entity) => Db.Set<T>().Remove(entity);
    public Task<int> SaveAsync(CancellationToken ct = default) => Db.SaveChangesAsync(ct);
}

public static class DbSetExtensions
{
    /// <summary>Finds by id or throws the standard 404 domain exception.</summary>
    public static async Task<T> FindOrThrowAsync<T>(this IQueryable<T> set, int id, string what) where T : BaseEntity =>
        await set.FirstOrDefaultAsync(e => e.Id == id) ?? throw new Application.Exceptions.NotFoundException(what);
}

public interface ITicketRepository : IGenericRepository<Ticket>
{
    /// <summary>Base query incl. all navigation properties, with role-based access filter applied.</summary>
    IQueryable<Ticket> VisibleTo(bool isStaff, int userId, int? agentId);
}