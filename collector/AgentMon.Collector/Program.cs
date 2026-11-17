using System.Diagnostics;
using System.Net.Http.Json;

var key = Environment.GetEnvironmentVariable("AGENTMON_API_KEY") ?? throw new InvalidOperationException("Missing API key.");
var backend = Environment.GetEnvironmentVariable("AGENTMON_ENDPOINT") ?? "http://127.0.0.1:5088";
var agent = Environment.GetEnvironmentVariable("AGENTMON_AGENT_ID") ?? Environment.MachineName;
var targets = (Environment.GetEnvironmentVariable("AGENTMON_TARGETS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(x => new Uri(x, UriKind.Absolute)).ToArray();
if (targets.Length == 0 || targets.Any(x => x.Scheme != "https"))
    throw new InvalidOperationException("Set AGENTMON_TARGETS to approved HTTPS URLs.");
if (!Uri.TryCreate(backend, UriKind.Absolute, out var backendUri) ||
    (backendUri.Scheme != "https" && !(backendUri.IsLoopback && backendUri.Scheme == "http")))
    throw new InvalidOperationException("Backend must use HTTPS except for local testing.");

using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
using var ingest = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
ingest.DefaultRequestHeaders.Add("X-AgentMon-Key", key);
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
while (!cancel.IsCancellationRequested)
{
    foreach (var target in targets)
    {
        var watch = Stopwatch.StartNew();
        var reachable = false;
        string? error = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, target);
            using var response = await probe.SendAsync(request, cancel.Token);
            reachable = (int)response.StatusCode < 500;
            if (!reachable) error = "HTTP_" + (int)response.StatusCode;
        }
        catch (HttpRequestException) { error = "CONNECTION_ERROR"; }
        catch (TaskCanceledException) when (!cancel.IsCancellationRequested) { error = "TIMEOUT"; }
        watch.Stop();
        var sample = new { AgentId = agent, Target = target.Host, Timestamp = DateTimeOffset.UtcNow,
            Reachable = reachable, LatencyMs = (double?)watch.Elapsed.TotalMilliseconds, Error = error };
        try
        {
            using var result = await ingest.PostAsJsonAsync(new Uri(backendUri, "/api/v1/samples"), sample, cancel.Token);
            result.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException) { Console.Error.WriteLine("Telemetry delivery failed."); }
    }
    try { await Task.Delay(TimeSpan.FromSeconds(5), cancel.Token); }
    catch (OperationCanceledException) { break; }
}
