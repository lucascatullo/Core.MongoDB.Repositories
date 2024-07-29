using MongoDB.Bson.Serialization.Attributes;

namespace Core.MongoDB.Repositories.Models;

public class MongoModel : IMongoModel
{
    [BsonId]
    public string? _id { get; set; } = DateTime.UtcNow.Ticks.ToString();


    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;


    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

}