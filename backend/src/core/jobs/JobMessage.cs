using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetSvelte.Core.Jobs;

public static class JobProtocol
{
    public const string QueueKey = "foxg:jobs";
    public const int MaxRetries = 3;
}

public sealed record JobMessage(string Id, string Task, JsonElement Args, DateTime EnqueuedAt, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Attempt = null);
