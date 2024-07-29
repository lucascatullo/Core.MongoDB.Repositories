namespace Core.MongoDB.Repositories.Models;

public interface IMongoModel
{
    string? _id { get; set; }
    DateTime CreatedDate { get; set; }
    DateTime ModifiedDate { get; set; }
}