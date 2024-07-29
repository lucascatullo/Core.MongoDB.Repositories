using Core.MongoDB.Repositories.Models;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace Core.MongoDB.Repositories.Repositories;

public abstract class MongoRepository<T> : IMongoRepository<T> where T : MongoModel
{
    protected IList<T> addedObjects = new List<T>();

    protected IList<T> editedObjects = new List<T>();

    protected IList<string> deletedObjects = new List<string>();

    private IMongoCollection<T> _collection;

    private bool ascending = true;

    private bool isSorted;

    private List<Expression<Func<T, bool>>> _whereExpressions = new();

    private Expression<Func<T, object>>? _sortExpression;

    protected MongoRepository(string connectionString, string databaseName, string collectionName)
    {
        MongoClientSettings settings = MongoClientSettings.FromConnectionString(connectionString);
        MongoClient mongoClient = new MongoClient(settings);
        IMongoDatabase database = mongoClient.GetDatabase(databaseName);
        if (databaseName == null)
        {
            throw new ArgumentException("Invalid Database name");
        }

        _collection = database.GetCollection<T>(collectionName);
        if (collectionName == null)
        {
            throw new ArgumentException("Collection not found");
        }
    }

    public virtual bool ValidateUpdate()
    {
        return !editedObjects.Any((e) => e._id == null);
    }

    public abstract bool ValidateCreate();

    public void Add(T add)
    {
        if (add == null)
        {
            throw new ArgumentNullException("add");
        }

        addedObjects.Add(add);
    }

    public void Update(T edited)
    {
        if (edited == null)
        {
            throw new ArgumentNullException("edited");
        }

        editedObjects.Add(edited);
    }

    public void Delete(string id)
    {
        deletedObjects.Add(id);
    }

    private async Task BulkAddToMongoDB()
    {
        if (ValidateCreate() && addedObjects.Count > 0)
        {
            if (addedObjects.Count == 1)
            {
                await _collection.InsertOneAsync(addedObjects.First());
            }
            else
            {
                await _collection.InsertManyAsync(addedObjects);
            }
        }
    }

    private async Task BulkUpdateToMOngoDB()
    {
        if (!ValidateUpdate() || editedObjects.Count <= 0)
        {
            return;
        }

        foreach (T item in editedObjects)
        {
            FilterDefinition<T> filter = Builders<T>.Filter.Eq("_id", item._id);
            item.ModifiedDate = DateTime.UtcNow;
            var resposne = await _collection.ReplaceOneAsync(filter, item);
        }
    }

    private async Task BulkDeleteToMongoDB()
    {
        foreach (string deletedKey in deletedObjects)
        {
            FilterDefinition<T> filter = Builders<T>.Filter.Eq("_id", deletedKey);
            await _collection.DeleteOneAsync(filter);
        }
    }

    public async Task SaveChangesAsync()
    {
        await BulkAddToMongoDB();
        await BulkUpdateToMOngoDB();
        await BulkDeleteToMongoDB();
    }

    public IMongoRepository<T> OrderBy(Expression<Func<T, object>> orderExpression)
    {
        isSorted = true;
        _sortExpression = orderExpression;
        return this;
    }

    public IMongoRepository<T> OrderByDescending(Expression<Func<T, object>> orderExpression)
    {
        isSorted = true;
        ascending = false;
        _sortExpression = orderExpression;
        return this;
    }

    private async Task<long> CountAsync()
    {
        return await GetFilter().CountDocumentsAsync();
    }

    public async Task<long> CountAsync(Expression<Func<T, bool>> countExpression)
    {
        Where(countExpression);
        return await GetFilter().CountDocumentsAsync();
    }

    public IMongoRepository<T> Where(Expression<Func<T, bool>> whereExpression)
    {
        _whereExpressions.Add(whereExpression);
        return this;
    }

    public async Task<IList<T>> ToListAsync()
    {
        return await GetFilter().ToListAsync();
    }

    public async Task<PageResponse<T>> Paginate(int pageSize, int pageNum)
    {
        PageResponse<T> response = new();
        response.Response = await GetFilter(pageNum, pageSize).ToListAsync();
        response.HasNextPage = await CountAsync() / pageSize > pageNum;
        return response;
    }

    public async Task<T> FirstAsync()
    {
        T response = await GetFilter().FirstOrDefaultAsync();
        if (response == null)
        {
            throw new Exception<NotFoundInQueryException>(new NotFoundInQueryException("T", "First", "Empty"));
        }

        return response;
    }

    public async Task<T> FirstAsync(Expression<Func<T, bool>> whereExpression)
    {
        Where(whereExpression);
        T response = await GetFilter().FirstOrDefaultAsync();
        if (response == null)
        {
            throw new Exception<NotFoundInQueryException>(new NotFoundInQueryException("T", "First", "Empty"));
        }

        return response;
    }

    public async Task<T> FirstOrDefaultAsync()
    {
        return await GetFilter().FirstOrDefaultAsync();
    }

    public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> whereExpression)
    {
        Where(whereExpression);
        return await GetFilter().FirstOrDefaultAsync();
    }

    public async Task<bool> AnyAsync()
    {
        return await _collection.Find(GetFilterDefinitions()).AnyAsync();
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> whereExpression)
    {
        return await _collection.Find(whereExpression).AnyAsync();
    }

    private FilterDefinition<T> GetFilterDefinitions()
    {
        FilterDefinitionBuilder<T> filter = Builders<T>.Filter;
        FilterDefinition<T> empty = filter.Empty;
        foreach (Expression<Func<T, bool>> whereExpression in _whereExpressions)
        {
            empty &= filter.And(whereExpression);
        }

        return empty;
    }

    private IFindFluent<T, T> GetFilter()
    {
        IFindFluent<T, T> findFluent = _collection.Find(GetFilterDefinitions());
        if (isSorted)
        {
            findFluent = ascending ? findFluent.SortBy(_sortExpression) : findFluent.SortByDescending(_sortExpression);
        }

        return findFluent;
    }

    private IFindFluent<T, T> GetFilter(int pageNum, int pageSize)
    {
        IFindFluent<T, T> findFluent = _collection.Find(GetFilterDefinitions()).Skip((pageNum - 1) * pageSize).Limit(pageSize);
        if (isSorted)
        {
            findFluent = ascending ? findFluent.SortBy(_sortExpression) : findFluent.SortByDescending(_sortExpression);
        }

        return findFluent;
    }

    public IMongoRepository<T> Filter(IFilter<string> filters)
    {
        if (filters != null)
        {
            if (filters.CreatedDateTo != null) ToDate(filters.CreatedDateTo.Value);
            if (filters.CreatedDateFrom != null) FromDate(filters.CreatedDateFrom.Value);
            if (filters.ModifiedDateFrom != null) FromModifiedDate(filters.ModifiedDateFrom.Value);
            if (filters.ModifiedDateTo != null) ToModifiedDate(filters.ModifiedDateTo.Value);
            if (filters.Ids != null) HasIds(filters.Ids);
            if (filters.Exclude != null) HasNotIds(filters.Exclude);
            if (filters.OrderByDate != null) OrderByDate(filters.OrderByDate.Value);
        }
        return this;
    }

    public IMongoRepository<T> FromDate(DateTime date)
    {
        Where(x => x.CreatedDate >= date);
        return this;
    }

    public IMongoRepository<T> ToDate(DateTime date)
    {
        Where(x => x.CreatedDate <= date);
        return this;
    }

    public IMongoRepository<T> FromModifiedDate(DateTime date)
    {
        Where(x => x.ModifiedDate >= date);
        return this;
    }
    public IMongoRepository<T> ToModifiedDate(DateTime date)
    {
        Where(x => x.ModifiedDate <= date);
        return this;
    }

    public IMongoRepository<T> HasIds(IEnumerable<string> ids)
    {
        Where(x => ids.Contains(x._id));
        return this;
    }
    public IMongoRepository<T> HasNotIds(IEnumerable<string> ids)
    {
        Where(x => !ids.Contains(x._id));
        return this;
    }

    public IMongoRepository<T> OrderByDate(OrderByDate order)
    {
        if (order == Ona.Common.BaseEFModels.OrderByDate.ASC)
            OrderBy(x => x.CreatedDate);
        else
            OrderByDescending(x => x.CreatedDate);
        return this;
    }

}