using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5088");
var app = builder.Build();
var key = Environment.GetEnvironmentVariable("AGENTMON_API_KEY");
if (string.IsNullOrEmpty(key) || key.Length < 32)
    throw new InvalidOperationException("Set AGENTMON_API_KEY to a random secret of at least 32 characters.");
var records = new ConcurrentDictionary<string, ConcurrentQueue<ProbeSample>>();

bool Auth(HttpRequest req)
{
    var supplied = req.Headers["X-AgentMon-Key"].ToString();
    if (string.IsNullOrEmpty(supplied)) return false;
    var actual = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
    var expected = SHA256.HashData(Encoding.UTF8.GetBytes(key));
    return CryptographicOperations.FixedTimeEquals(actual, expected);
}

app.MapPost("/api/v1/samples", (HttpRequest req, ProbeSample sample) =>
{
    if (!Auth(req)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(sample.AgentId) || sample.AgentId.Length > 128 ||
        string.IsNullOrWhiteSpace(sample.Target) || sample.Target.Length > 253 ||
        sample.LatencyMs is < 0 or > 120000 ||
        sample.Timestamp < DateTimeOffset.UtcNow.AddDays(-1) ||
        sample.Timestamp > DateTimeOffset.UtcNow.AddMinutes(5)) return Results.BadRequest();
    var queue = records.GetOrAdd(sample.AgentId, _ => new ConcurrentQueue<ProbeSample>());
    queue.Enqueue(sample);
    while (queue.Count > 120 && queue.TryDequeue(out _)) {}
    return Results.Accepted();
});
app.MapGet("/api/v1/agents/{agentId}/samples", (HttpRequest req, string agentId) =>
{
    if (!Auth(req)) return Results.Unauthorized();
    return Results.Ok(records.TryGetValue(agentId, out var queue) ? queue.ToArray() : Array.Empty<ProbeSample>());
});
app.Run();

public sealed record ProbeSample(string AgentId, string Target, DateTimeOffset Timestamp, bool Reachable, double? LatencyMs, string? Error);
