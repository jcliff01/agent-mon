using System.Collections.Concurrent;

namespace AgentMon.Server;

/// <summary>One HTTPS reachability measurement sent by a collector.</summary>
public sealed record ProbeSample(
    string AgentId, string Target, DateTimeOffset Timestamp, bool Reachable, double? LatencyMs, string? Error);

/// <summary>Latest sample time per agent, for the agent list.</summary>
public sealed record AgentSummary(string AgentId, DateTimeOffset LastSeen, int Samples);

/// <summary>Keeps the most recent samples for each agent in memory.</summary>
public sealed class SampleStore(TimeProvider clock, int samplesPerAgent = 120, int maxAgents = 5000)
{
    public const int MaxIdLength = 128;
    public const int MaxTargetLength = 253;
    public const int MaxErrorLength = 200;
    public const double MaxLatencyMs = 120_000;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<ProbeSample>> records =
        new(StringComparer.OrdinalIgnoreCase);

    public int SamplesPerAgent { get; } = samplesPerAgent;
    public int MaxAgents { get; } = maxAgents;

    /// <summary>Returns null when the sample is acceptable, otherwise a short reason.</summary>
    public string? Validate(ProbeSample? sample)
    {
        if (sample is null)
            return "missing body";
        if (!ValidId(sample.AgentId))
            return "agentId must be 1-128 letters, digits, '.', '-' or '_'";
        if (string.IsNullOrWhiteSpace(sample.Target) || sample.Target.Length > MaxTargetLength ||
            sample.Target.Any(char.IsControl))
            return "target must be 1-253 printable characters";
        if (sample.LatencyMs is { } latency && (!double.IsFinite(latency) || latency < 0 || latency > MaxLatencyMs))
            return "latencyMs must be between 0 and 120000";
        var now = clock.GetUtcNow();
        if (sample.Timestamp < now.AddDays(-1) || sample.Timestamp > now.AddMinutes(5))
            return "timestamp must be within the last day (and at most 5 minutes ahead)";
        if (sample.Error is { Length: > MaxErrorLength })
            return "error must be at most 200 characters";
        return null;
    }

    /// <summary>Agent IDs are used in URLs, so keep them to a safe character set.</summary>
    public static bool ValidId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= MaxIdLength &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    /// <summary>Adds a validated sample. Returns false when the agent limit is reached.</summary>
    public bool Add(ProbeSample sample)
    {
        if (!records.TryGetValue(sample.AgentId, out var queue))
        {
            if (records.Count >= MaxAgents)
                return false;
            queue = records.GetOrAdd(sample.AgentId, _ => new ConcurrentQueue<ProbeSample>());
        }
        queue.Enqueue(sample);
        while (queue.Count > SamplesPerAgent && queue.TryDequeue(out _)) { }
        return true;
    }

    public ProbeSample[] Get(string agentId) =>
        records.TryGetValue(agentId, out var queue) ? queue.ToArray() : [];

    public AgentSummary[] Agents() => records
        .Select(pair => (pair.Key, Samples: pair.Value.ToArray()))
        .Where(item => item.Samples.Length > 0)
        .Select(item => new AgentSummary(item.Key, item.Samples.Max(s => s.Timestamp), item.Samples.Length))
        .OrderBy(summary => summary.AgentId, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
