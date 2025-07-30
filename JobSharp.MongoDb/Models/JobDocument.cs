using JobSharp.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace JobSharp.MongoDb.Models;

/// <summary>
/// Represents a job document for MongoDB storage.
/// </summary>
public class JobDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public required string Id { get; set; }

    [BsonElement("typeName")]
    public required string TypeName { get; set; }

    [BsonElement("arguments")]
    public string? Arguments { get; set; }

    [BsonElement("state")]
    [BsonRepresentation(BsonType.Int32)]
    public JobState State { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("scheduledAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ScheduledAt { get; set; }

    [BsonElement("executedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ExecutedAt { get; set; }

    [BsonElement("retryCount")]
    public int RetryCount { get; set; }

    [BsonElement("maxRetryCount")]
    public int MaxRetryCount { get; set; }

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("result")]
    public string? Result { get; set; }

    [BsonElement("batchId")]
    public string? BatchId { get; set; }

    [BsonElement("parentJobId")]
    public string? ParentJobId { get; set; }
}