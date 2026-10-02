using System.Text.Json;
using DotnetSvelte.Core.Cache;
using DotnetSvelte.Core.Config;
using DotnetSvelte.Core.Errors;

namespace DotnetSvelte.Core.Jobs;

public sealed class RedisJobQueue(RedisConnection redis) : IJobQueue
{
    public async Task<string> EnqueueAsync(string task, object args)
    {
        var job = new JobMessage(
            Guid.NewGuid().ToString(),
            task,
            JsonSerializer.SerializeToElement(args, Json.Options),
            DateTime.UtcNow);
        try
        {
            var db = await redis.GetDatabaseAsync();
            await db.ListLeftPushAsync(JobProtocol.QueueKey, JsonSerializer.Serialize(job, Json.Options));
        }
        catch (Exception ex)
        {
            throw AppException.Unavailable($"Redis unavailable: {Summary(ex)}");
        }
        return job.Id;
    }

    private static string Summary(Exception ex) => ex.Message.Split(',', ';', (char)10)[0].Trim();
}
