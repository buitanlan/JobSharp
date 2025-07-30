using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace JobSharp.MongoDb.Models;

/// <summary>
/// Represents a recurring job document for MongoDB storage.
/// </summary>
public class RecurringJobDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public required string Id { get; set; }

    [BsonElement("cronExpression")]
    public required string CronExpression { get; set; }

    [BsonElement("jobTypeName")]
    public required string JobTypeName { get; set; }

    [BsonElement("jobArguments")]
    public string? JobArguments { get; set; }

    [BsonElement("maxRetryCount")]
    public int MaxRetryCount { get; set; }

    [BsonElement("nextExecution")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? NextExecution { get; set; }

    [BsonElement("lastExecution")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastExecution { get; set; }

    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
}