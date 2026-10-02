using System.Text.Json;
using DotnetSvelte.Core.Cache;
using DotnetSvelte.Core.Config;
using DotnetSvelte.Core.Jobs;
using StackExchange.Redis;

namespace DotnetSvelte.Worker;

/// <summary>BRPOP loop over the shared Redis list; failed jobs are re-pushed with an incremented attempt.</summary>
public sealed class JobWorker(RedisConnection redis, JobTasks tasks, ILogger<JobWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        log.LogInformation("worker listening on {Queue}", JobProtocol.QueueKey);

        while (!stop.IsCancellationRequested)
        {
            try
            {
                var db = await redis.GetDatabaseAsync();
                var popped = await db.ExecuteAsync("BRPOP", JobProtocol.QueueKey, 5);
                if (popped.IsNull) continue;

                var payload = (string?)((RedisResult[])popped!)[1];
                if (payload is not null) await HandleAsync(db, payload);
            }
            catch (Exception ex) when (!stop.IsCancellationRequested)
            {
                log.LogWarning("redis unavailable: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), stop);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task HandleAsync(IDatabase db, string payload)
    {
        JobMessage? job;
        try
        {
            job = JsonSerializer.Deserialize<JobMessage>(payload, Json.Options);
        }
        catch (JsonException ex)
        {
            log.LogError("dropping malformed job: {Message}", ex.Message);
            return;
        }

        if (job is null || !tasks.TryGet(job.Task, out var run))
        {
            log.LogWarning("dropping job with unknown task {Task}", job?.Task);
            return;
        }

        try
        {
            await run(job.Args);
        }
        catch (Exception ex)
        {
            var attempt = (job.Attempt ?? 0) + 1;
            if (attempt > JobProtocol.MaxRetries)
            {
                log.LogError(ex, "job {Id} ({Task}) failed {Attempt} times, dropping", job.Id, job.Task, attempt);
                return;
            }

            log.LogWarning("job {Id} ({Task}) failed: {Message}; retry {Attempt}/{Max}",
                job.Id, job.Task, ex.Message, attempt, JobProtocol.MaxRetries);
            await db.ListLeftPushAsync(JobProtocol.QueueKey,
                JsonSerializer.Serialize(job with { Attempt = attempt }, Json.Options));
        }
    }
}
