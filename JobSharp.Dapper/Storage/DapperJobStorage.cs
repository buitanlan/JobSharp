using System.Data;
using System.Globalization;
using JobSharp.Core;
using JobSharp.Dapper.Models;
using JobSharp.Jobs;
using JobSharp.Storage;
using Dapper;
using Microsoft.Extensions.Logging;

namespace JobSharp.Dapper.Storage;

/// <summary>
/// Custom TypeHandler for DateTimeOffset to handle string conversion in SQLite.
/// </summary>
public class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.Value = value.ToString("O"); // Use ISO 8601 format
    }

    public override DateTimeOffset Parse(object value)
    {
        if (value is string stringValue)
        {
            if (DateTimeOffset.TryParse(stringValue, out var result))
                return result;
        }

        if (value is DateTimeOffset dateTimeOffsetValue)
            return dateTimeOffsetValue;

        throw new InvalidCastException($"Cannot convert {value?.GetType()?.Name ?? "null"} to DateTimeOffset");
    }
}

/// <summary>
/// Dapper implementation of IJobStorage.
/// </summary>
public class DapperJobStorage : IJobStorage
{
    private readonly IDbConnection _connection;
    private readonly ILogger<DapperJobStorage> _logger;

    static DapperJobStorage()
    {
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
    }

    public DapperJobStorage(IDbConnection connection, ILogger<DapperJobStorage> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> StoreJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO Jobs (Id, TypeName, Arguments, State, CreatedAt, ScheduledAt, ExecutedAt, RetryCount, MaxRetryCount, ErrorMessage, Result, BatchId, ParentJobId)
            VALUES (@Id, @TypeName, @Arguments, @State, @CreatedAt, @ScheduledAt, @ExecutedAt, @RetryCount, @MaxRetryCount, @ErrorMessage, @Result, @BatchId, @ParentJobId)";

        var model = MapToModel(job);
        await _connection.ExecuteAsync(sql, model);

        _logger.LogDebug("Stored job {JobId} in database", job.Id);
        return job.Id;
    }

    public async Task UpdateJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Jobs 
            SET Arguments = @Arguments, State = @State, ScheduledAt = @ScheduledAt, ExecutedAt = @ExecutedAt, 
                RetryCount = @RetryCount, MaxRetryCount = @MaxRetryCount, ErrorMessage = @ErrorMessage, 
                Result = @Result, BatchId = @BatchId, ParentJobId = @ParentJobId
            WHERE Id = @Id";

        var model = MapToModel(job);
        var rowsAffected = await _connection.ExecuteAsync(sql, model);

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Job with ID {job.Id} not found");
        }

        _logger.LogDebug("Updated job {JobId} in database", job.Id);
    }

    public async Task<IJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Jobs WHERE Id = @Id";

        var model = await _connection.QuerySingleOrDefaultAsync<JobModel>(sql, new { Id = jobId });
        return model != null ? MapFromModel(model) : null;
    }

    public async Task<IEnumerable<IJob>> GetScheduledJobsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var sql = GetPaginatedQuery(@"
            SELECT * FROM Jobs 
            WHERE State = @State AND ScheduledAt <= @Now
            ORDER BY ScheduledAt", batchSize);

        var now = DateTimeOffset.UtcNow;
        var models = await _connection.QueryAsync<JobModel>(sql, new
        {
            State = (int)JobState.Scheduled,
            Now = now,
            BatchSize = batchSize
        });

        return models.Select(model => MapFromModel(model));
    }

    public async Task<IEnumerable<IJob>> GetJobsByStateAsync(JobState state, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var sql = GetPaginatedQuery(@"
            SELECT * FROM Jobs 
            WHERE State = @State
            ORDER BY CreatedAt", batchSize);

        var models = await _connection.QueryAsync<JobModel>(sql, new
        {
            State = (int)state,
            BatchSize = batchSize
        });

        return models.Select(model => MapFromModel(model));
    }

    public async Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Jobs WHERE Id = @Id";

        var rowsAffected = await _connection.ExecuteAsync(sql, new { Id = jobId });
        if (rowsAffected > 0)
        {
            _logger.LogDebug("Deleted job {JobId} from database", jobId);
        }
    }

    public async Task<int> GetJobCountAsync(JobState state, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM Jobs WHERE State = @State";

        return await _connection.QuerySingleAsync<int>(sql, new { State = (int)state });
    }

    public async Task StoreBatchAsync(string batchId, IEnumerable<IJob> jobs, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO Jobs (Id, TypeName, Arguments, State, CreatedAt, ScheduledAt, ExecutedAt, RetryCount, MaxRetryCount, ErrorMessage, Result, BatchId, ParentJobId)
            VALUES (@Id, @TypeName, @Arguments, @State, @CreatedAt, @ScheduledAt, @ExecutedAt, @RetryCount, @MaxRetryCount, @ErrorMessage, @Result, @BatchId, @ParentJobId)";

        var models = jobs.Select(job => MapToModel(job)).ToList();
        await _connection.ExecuteAsync(sql, models);

        _logger.LogDebug("Stored batch {BatchId} with {JobCount} jobs in database", batchId, models.Count);
    }

    public async Task<IEnumerable<IJob>> GetBatchJobsAsync(string batchId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Jobs WHERE BatchId = @BatchId";

        var models = await _connection.QueryAsync<JobModel>(sql, new { BatchId = batchId });
        return models.Select(model => MapFromModel(model));
    }

    public async Task StoreContinuationAsync(string parentJobId, IJob continuationJob, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO Jobs (Id, TypeName, Arguments, State, CreatedAt, ScheduledAt, ExecutedAt, RetryCount, MaxRetryCount, ErrorMessage, Result, BatchId, ParentJobId)
            VALUES (@Id, @TypeName, @Arguments, @State, @CreatedAt, @ScheduledAt, @ExecutedAt, @RetryCount, @MaxRetryCount, @ErrorMessage, @Result, @BatchId, @ParentJobId)";

        var model = MapToModel(continuationJob);
        model.ParentJobId = parentJobId;

        await _connection.ExecuteAsync(sql, model);

        _logger.LogDebug("Stored continuation job {JobId} for parent {ParentJobId} in database",
            continuationJob.Id, parentJobId);
    }

    public async Task<IEnumerable<IJob>> GetContinuationsAsync(string parentJobId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM Jobs 
            WHERE ParentJobId = @ParentJobId AND State = @State";

        var models = await _connection.QueryAsync<JobModel>(sql, new
        {
            ParentJobId = parentJobId,
            State = (int)JobState.AwaitingContinuation
        });

        return models.Select(model => MapFromModel(model));
    }

    public async Task StoreRecurringJobAsync(string recurringJobId, string cronExpression, IJob jobTemplate, CancellationToken cancellationToken = default)
    {
        var model = new RecurringJobModel
        {
            Id = recurringJobId,
            CronExpression = cronExpression,
            JobTypeName = jobTemplate.TypeName,
            JobArguments = jobTemplate.Arguments,
            MaxRetryCount = jobTemplate.MaxRetryCount,
            CreatedAt = DateTimeOffset.UtcNow,
            IsEnabled = true
        };

        // Check if exists first
        const string existsSql = "SELECT COUNT(*) FROM RecurringJobs WHERE Id = @Id";
        var exists = await _connection.QuerySingleAsync<int>(existsSql, new { Id = recurringJobId }) > 0;

        if (exists)
        {
            const string updateSql = @"
                UPDATE RecurringJobs 
                SET CronExpression = @CronExpression, JobTypeName = @JobTypeName, 
                    JobArguments = @JobArguments, MaxRetryCount = @MaxRetryCount
                WHERE Id = @Id";
            await _connection.ExecuteAsync(updateSql, model);
        }
        else
        {
            const string insertSql = @"
                INSERT INTO RecurringJobs (Id, CronExpression, JobTypeName, JobArguments, MaxRetryCount, CreatedAt, IsEnabled)
                VALUES (@Id, @CronExpression, @JobTypeName, @JobArguments, @MaxRetryCount, @CreatedAt, @IsEnabled)";
            await _connection.ExecuteAsync(insertSql, model);
        }

        _logger.LogDebug("Stored/updated recurring job {RecurringJobId} in database", recurringJobId);
    }

    public async Task<IEnumerable<RecurringJobInfo>> GetRecurringJobsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM RecurringJobs WHERE IsEnabled = 1";

        var models = await _connection.QueryAsync<RecurringJobModel>(sql);

        return models.Select(model => new RecurringJobInfo
        {
            Id = model.Id,
            CronExpression = model.CronExpression,
            JobTemplate = new Job
            {
                Id = Guid.NewGuid().ToString(),
                TypeName = model.JobTypeName,
                Arguments = model.JobArguments,
                MaxRetryCount = model.MaxRetryCount,
                State = JobState.Created
            },
            NextExecution = model.NextExecution,
            LastExecution = model.LastExecution,
            IsEnabled = model.IsEnabled,
            CreatedAt = model.CreatedAt
        });
    }

    public async Task RemoveRecurringJobAsync(string recurringJobId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM RecurringJobs WHERE Id = @Id";

        var rowsAffected = await _connection.ExecuteAsync(sql, new { Id = recurringJobId });
        if (rowsAffected > 0)
        {
            _logger.LogDebug("Removed recurring job {RecurringJobId} from database", recurringJobId);
        }
    }

    private static JobModel MapToModel(IJob job)
    {
        var model = new JobModel
        {
            Id = job.Id,
            TypeName = job.TypeName,
            Arguments = job.Arguments,
            State = job.State,
            CreatedAt = job.CreatedAt,
            ScheduledAt = job.ScheduledAt,
            ExecutedAt = job.ExecutedAt,
            RetryCount = job.RetryCount,
            MaxRetryCount = job.MaxRetryCount,
            ErrorMessage = job.ErrorMessage,
            Result = job.Result
        };

        if (job is Job jobImpl)
        {
            model.BatchId = jobImpl.BatchId;
            model.ParentJobId = jobImpl.ParentJobId;
        }

        return model;
    }

    private static Job MapFromModel(JobModel model)
    {
        return new Job
        {
            Id = model.Id,
            TypeName = model.TypeName,
            Arguments = model.Arguments,
            State = model.State,
            CreatedAt = model.CreatedAt,
            ScheduledAt = model.ScheduledAt,
            ExecutedAt = model.ExecutedAt,
            RetryCount = model.RetryCount,
            MaxRetryCount = model.MaxRetryCount,
            ErrorMessage = model.ErrorMessage,
            Result = model.Result,
            BatchId = model.BatchId,
            ParentJobId = model.ParentJobId
        };
    }

    private string GetPaginatedQuery(string baseQuery, int batchSize)
    {
        // Determine database provider type from connection
        var connectionType = _connection.GetType().Name;

        return connectionType switch
        {
            "SqlConnection" => $"{baseQuery} OFFSET 0 ROWS FETCH NEXT @BatchSize ROWS ONLY",
            "MySqlConnection" => $"{baseQuery} LIMIT @BatchSize",
            "NpgsqlConnection" => $"{baseQuery} LIMIT @BatchSize",
            "SqliteConnection" => $"{baseQuery} LIMIT @BatchSize",
            _ => $"{baseQuery} LIMIT @BatchSize" // Default to LIMIT syntax
        };
    }
}