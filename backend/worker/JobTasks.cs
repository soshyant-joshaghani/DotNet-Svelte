using System.Text.Json;

namespace DotnetSvelte.Worker;

/// <summary>Task registry: add new background tasks to the dictionary by name.</summary>
public sealed class JobTasks
{
    private readonly Dictionary<string, Func<JsonElement, Task>> _tasks;

    public JobTasks(ILogger<JobTasks> log)
    {
        _tasks = new()
        {
            ["ping"] = args =>
            {
                var message = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("message", out var m)
                    ? m.GetString()
                    : "ping";
                log.LogInformation("ping job received: {Message}", message);
                return Task.CompletedTask;
            },
        };
    }

    public bool TryGet(string name, out Func<JsonElement, Task> task) => _tasks.TryGetValue(name, out task!);
}
