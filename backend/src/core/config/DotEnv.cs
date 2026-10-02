using Microsoft.Extensions.Configuration;

namespace DotnetSvelte.Core.Config;

public static class DotEnv
{
    private static readonly string[] SearchPaths = [".", "..", Path.Combine("..", "..")];

    public static IConfigurationBuilder AddDotEnv(this IConfigurationBuilder builder)
    {
        var values = Load();
        if (values.Count > 0) builder.Sources.Insert(0, new DotEnvSource(values));
        return builder;
    }

    public static Dictionary<string, string?> Load()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var file = SearchPaths
            .Select(p => Path.GetFullPath(Path.Combine(p, ".env")))
            .FirstOrDefault(File.Exists);
        if (file is null) return values;

        foreach (var raw in File.ReadLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == (char)39) && value[^1] == value[0])
                value = value[1..^1];
            values[key] = value;
        }

        return values;
    }

    private sealed class DotEnvSource(Dictionary<string, string?> values) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new DotEnvProvider(values);
    }

    private sealed class DotEnvProvider : ConfigurationProvider
    {
        public DotEnvProvider(Dictionary<string, string?> values) => Data = values;
    }
}
