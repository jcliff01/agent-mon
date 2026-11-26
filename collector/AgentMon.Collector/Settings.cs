namespace AgentMon.Collector;

/// <summary>A system to test, with the name the dashboard shows for it.</summary>
public sealed record Target(string Name, Uri Url);

/// <summary>Collector settings read from AGENTMON_* environment variables.</summary>
public sealed record Settings(string Key, Uri Server, string AgentId, IReadOnlyList<Target> Targets, TimeSpan Interval)
{
    public const int MinimumKeyLength = 32;

    /// <summary>Reads and checks the settings. On failure returns null and a message saying what to fix.</summary>
    public static Settings? Load(Func<string, string?> env, string machineName, out string? problem)
    {
        problem = null;
        string? key = env("AGENTMON_API_KEY");
        if (string.IsNullOrEmpty(key) || key.Length < MinimumKeyLength)
        {
            problem = $"Set AGENTMON_API_KEY to the server's secret key (at least {MinimumKeyLength} characters).";
            return null;
        }

        var targets = ParseTargets(env("AGENTMON_TARGETS"), out problem);
        if (targets is null)
            return null;

        string serverText = env("AGENTMON_ENDPOINT") ?? "http://127.0.0.1:5088";
        if (!Uri.TryCreate(serverText, UriKind.Absolute, out var server) ||
            (server.Scheme != Uri.UriSchemeHttps && !(server.Scheme == Uri.UriSchemeHttp && server.IsLoopback)))
        {
            problem = "AGENTMON_ENDPOINT must be an https:// address (http:// is only allowed for this computer).";
            return null;
        }

        string agentId = env("AGENTMON_AGENT_ID") ?? machineName;
        if (!ValidId(agentId))
        {
            problem = "AGENTMON_AGENT_ID must be 1-128 letters, digits, '.', '-' or '_'.";
            return null;
        }

        var interval = TimeSpan.FromSeconds(5);
        if (env("AGENTMON_INTERVAL_SECONDS") is { Length: > 0 } text)
        {
            if (!int.TryParse(text, out int seconds) || seconds is < 1 or > 300)
            {
                problem = "AGENTMON_INTERVAL_SECONDS must be a whole number from 1 to 300.";
                return null;
            }
            interval = TimeSpan.FromSeconds(seconds);
        }

        return new Settings(key, server, agentId, targets, interval);
    }

    public static bool ValidId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 128 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    /// <summary>
    /// Parses comma-separated targets, each optionally named: <c>Nest=https://nest.example/health,https://finesse.example/</c>.
    /// A target without a name is shown by its host name.
    /// </summary>
    public static IReadOnlyList<Target>? ParseTargets(string? value, out string? problem)
    {
        problem = null;
        var targets = new List<Target>();
        foreach (string entry in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equals = entry.IndexOf('=');
            bool named = equals > 0 && !entry[..equals].Contains("://");
            string urlText = named ? entry[(equals + 1)..].Trim() : entry;
            if (!Uri.TryCreate(urlText, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            {
                problem = $"AGENTMON_TARGETS: \"{entry}\" isn't an https:// address. Use Name=https://address, separated by commas.";
                return null;
            }
            string name = named ? entry[..equals].Trim() : url.Host;
            if (name.Length > 253)
                name = name[..253];
            if (targets.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                problem = $"AGENTMON_TARGETS: the name \"{name}\" is used twice. Give each target its own name.";
                return null;
            }
            targets.Add(new Target(name, url));
        }
        if (targets.Count == 0)
        {
            problem = "Set AGENTMON_TARGETS to the systems to test, for example Finesse=https://finesse.example.com/";
            return null;
        }
        if (targets.Count > 50)
        {
            problem = "AGENTMON_TARGETS: at most 50 targets.";
            return null;
        }
        return targets;
    }
}
