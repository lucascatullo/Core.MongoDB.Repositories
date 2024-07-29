using Core.MongoDB.Repositories.Models;
using System.Linq.Expressions;

namespace Core.MongoDB.Repositories.Repositories;

public interface IMongoRepository<T> where T : MongoModel
{
    void Add(T add);

    Task<bool> AnyAsync();

    Task<bool> AnyAsync(Expression<Func<T, bool>> whereExpression);

    Task<long> CountAsync(Expression<Func<T, bool>> countExpression);

    void Delete(string id);

    Task<T> FirstAsync();

    Task<T> FirstAsync(Expression<Func<T, bool>> whereExpression);

    Task<T> FirstOrDefaultAsync();

    Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> whereExpression);

    IMongoRepository<T> OrderBy(Expression<Func<T, object>> orderExpression);

    IMongoRepository<T> OrderByDescending(Expression<Func<T, object>> orderExpression);

    Task<PageResponse<T>> Paginate(int pageSize, int pageNum);

    Task SaveChangesAsync();

    Task<IList<T>> ToListAsync();

    void Update(T edited);

    bool ValidateCreate();

    bool ValidateUpdate();

    IMongoRepository<T> Where(Expression<Func<T, bool>> whereExpression);
    IMongoRepository<T> OrderByDate(OrderByDate order);
}