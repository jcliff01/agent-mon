using AgentMon.Server;
using Microsoft.Extensions.FileProviders;

var keys = Keys.Load(
    Environment.GetEnvironmentVariable("AGENTMON_API_KEY"),
    Environment.GetEnvironmentVariable("AGENTMON_READ_KEY"),
    out string? problem);
if (keys is null)
{
    Console.Error.WriteLine("Agent Mon server can't start: " + problem);
    return 2;
}

// Read appsettings.json from the program's own folder, wherever it's started from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("AGENTMON_URLS") ?? "http://127.0.0.1:5088");
// Samples are tiny; refuse anything large.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(services => new SampleStore(services.GetRequiredService<TimeProvider>()));
var app = builder.Build();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "no-referrer";
    headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:";
    headers.CacheControl = "no-store";
    await next();
});

string? Supplied(HttpRequest request) => request.Headers[Keys.Header].ToString();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/v1/samples", (HttpRequest request, ProbeSample? sample, SampleStore store, ILogger<Program> log) =>
{
    if (!keys.CanWrite(Supplied(request)))
        return Results.Unauthorized();
    if (store.Validate(sample) is { } reason)
        return Results.BadRequest(new { error = reason });
    if (!store.Add(sample!))
    {
        log.LogWarning("Refused a sample from a new agent: the limit of {Max} agents is reached.", store.MaxAgents);
        return Results.StatusCode(StatusCodes.Status507InsufficientStorage);
    }
    return Results.Accepted();
});

app.MapGet("/api/v1/agents", (HttpRequest request, SampleStore store) =>
    keys.CanRead(Supplied(request)) ? Results.Ok(store.Agents()) : Results.Unauthorized());

app.MapGet("/api/v1/agents/{agentId}/samples", (HttpRequest request, string agentId, SampleStore store) =>
{
    if (!keys.CanRead(Supplied(request)))
        return Results.Unauthorized();
    if (!SampleStore.ValidId(agentId))
        return Results.BadRequest(new { error = "invalid agent ID" });
    return Results.Ok(store.Get(agentId));
});

// The dashboard: the same page as the Finesse gadget, which asks for the read key and polls.
string dashboard = Environment.GetEnvironmentVariable("AGENTMON_DASHBOARD_DIR") ??
    Path.Combine(AppContext.BaseDirectory, "dashboard");
if (Directory.Exists(dashboard))
{
    app.UseFileServer(new FileServerOptions
    {
        FileProvider = new PhysicalFileProvider(Path.GetFullPath(dashboard)),
        RequestPath = "/dashboard"
    });
    app.MapGet("/", () => Results.Redirect("/dashboard/"));
}

app.Logger.LogInformation("Agent Mon server {Version} started. Read-only key {ReadKey}.",
    typeof(Program).Assembly.GetName().Version?.ToString(3),
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENTMON_READ_KEY")) ? "not set" : "set");
app.Run();
return 0;

public partial class Program;
