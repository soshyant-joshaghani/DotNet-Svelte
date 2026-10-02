namespace DotnetSvelte.Core.Jobs;

public interface IJobQueue
{
    /// <summary>Pushes a job and returns its id. Throws an unavailable AppException when the queue is unreachable.</summary>
    Task<string> EnqueueAsync(string task, object args);
}
