using System.Diagnostics;
using System.Net.Http.Json;
using AgentMon.Collector;

var settings = Settings.Load(Environment.GetEnvironmentVariable, Environment.MachineName, out string? problem);
if (settings is null)
{
    Console.Error.WriteLine("Agent Mon collector can't start: " + problem);
    return 2;
}

string version = typeof(Settings).Assembly.GetName().Version?.ToString(3) ?? "0";
using var probe = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
{
    Timeout = TimeSpan.FromSeconds(5)
};
probe.DefaultRequestHeaders.UserAgent.ParseAdd($"AgentMon-Collector/{version}");
using var ingest = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
ingest.DefaultRequestHeaders.Add("X-AgentMon-Key", settings.Key);
ingest.DefaultRequestHeaders.UserAgent.ParseAdd($"AgentMon-Collector/{version}");
var samplesUri = new Uri(settings.Server, "/api/v1/samples");

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => cancel.Cancel();

Console.WriteLine($"Agent Mon collector {version}: testing {settings.Targets.Count} target(s) every " +
    $"{settings.Interval.TotalSeconds:0} s as \"{settings.AgentId}\", reporting to {settings.Server}. Press Ctrl+C to stop.");

bool deliveryFailing = false;
using var timer = new PeriodicTimer(settings.Interval);
do
{
    // All targets are tested at the same time, so one slow system doesn't delay the others.
    var samples = await Task.WhenAll(settings.Targets.Select(target => Measure(target, cancel.Token)));
    if (cancel.IsCancellationRequested)
        break;
    var results = await Task.WhenAll(samples.Select(sample => Deliver(sample, cancel.Token)));
    bool ok = results.All(r => r);
    if (!ok && !deliveryFailing)
        Console.Error.WriteLine($"{DateTime.Now:T} Can't deliver results to the server. Is it running, and do both use the same key? Retrying.");
    else if (ok && deliveryFailing)
        Console.WriteLine($"{DateTime.Now:T} Delivering results again.");
    deliveryFailing = !ok;
}
while (await Next(timer, cancel.Token));

Console.WriteLine("Stopped.");
return 0;

async Task<object> Measure(Target target, CancellationToken token)
{
    var watch = Stopwatch.StartNew();
    bool reachable = false;
    string? error = null;
    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, target.Url);
        using var response = await probe.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        // Any answer below 500 means the system is up, even if it refuses HEAD or wants a sign-in.
        reachable = (int)response.StatusCode < 500;
        if (!reachable)
            error = "HTTP_" + (int)response.StatusCode;
    }
    catch (HttpRequestException exception)
    {
        error = exception.HttpRequestError switch
        {
            HttpRequestError.NameResolutionError => "DNS_ERROR",
            HttpRequestError.SecureConnectionError => "TLS_ERROR",
            _ => "CONNECTION_ERROR"
        };
    }
    catch (TaskCanceledException) when (!token.IsCancellationRequested) { error = "TIMEOUT"; }
    catch (OperationCanceledException) { }
    watch.Stop();
    return new
    {
        AgentId = settings.AgentId,
        Target = target.Name,
        Timestamp = DateTimeOffset.UtcNow,
        Reachable = reachable,
        LatencyMs = reachable ? watch.Elapsed.TotalMilliseconds : (double?)null,
        Error = error
    };
}

async Task<bool> Deliver(object sample, CancellationToken token)
{
    try
    {
        using var response = await ingest.PostAsJsonAsync(samplesUri, sample, token);
        return response.IsSuccessStatusCode;
    }
    catch (HttpRequestException) { return false; }
    catch (TaskCanceledException) when (!token.IsCancellationRequested) { return false; }
    catch (OperationCanceledException) { return false; }
}

static async Task<bool> Next(PeriodicTimer timer, CancellationToken token)
{
    try { return await timer.WaitForNextTickAsync(token); }
    catch (OperationCanceledException) { return false; }
}
