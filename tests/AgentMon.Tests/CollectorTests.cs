using AgentMon.Collector;
using Xunit;

namespace AgentMon.Tests;

public class SettingsTests
{
    private static readonly string Key = new('k', 32);

    private static Settings? Load(out string? problem, params (string Name, string Value)[] values)
    {
        var env = values.ToDictionary(v => v.Name, v => v.Value);
        if (!env.ContainsKey("AGENTMON_API_KEY"))
            env["AGENTMON_API_KEY"] = Key;
        return Settings.Load(name => env.GetValueOrDefault(name), "MY-PC", out problem);
    }

    [Fact]
    public void LoadsDefaults()
    {
        var settings = Load(out var problem, ("AGENTMON_TARGETS", "Finesse=https://finesse.example.com/"));
        Assert.Null(problem);
        Assert.NotNull(settings);
        Assert.Equal("MY-PC", settings!.AgentId);
        Assert.Equal(new Uri("http://127.0.0.1:5088"), settings.Server);
        Assert.Equal(TimeSpan.FromSeconds(5), settings.Interval);
        Assert.Equal("Finesse", Assert.Single(settings.Targets).Name);
    }

    [Fact]
    public void ParsesNamedAndUnnamedTargets()
    {
        var targets = Settings.ParseTargets(
            "Nest=https://nest.example.com/health, https://finesse.example.com/?a=b", out var problem)!;
        Assert.Null(problem);
        Assert.Equal(new[] { "Nest", "finesse.example.com" }, targets.Select(t => t.Name).ToArray());
        Assert.Equal("https://finesse.example.com/?a=b", targets[1].Url.ToString());
    }

    [Fact]
    public void RejectsBadTargets()
    {
        Assert.Null(Settings.ParseTargets("", out var problem));
        Assert.NotNull(problem);
        Assert.Null(Settings.ParseTargets("Finesse=http://finesse.example.com/", out _));
        Assert.Null(Settings.ParseTargets("not a url", out _));
        Assert.Null(Settings.ParseTargets("A=https://a.example/,A=https://b.example/", out _));
    }

    [Fact]
    public void RejectsBadSettings()
    {
        const string Targets = "https://a.example/";
        Assert.Null(Load(out _, ("AGENTMON_API_KEY", "short"), ("AGENTMON_TARGETS", Targets)));
        Assert.Null(Load(out _, ("AGENTMON_TARGETS", Targets), ("AGENTMON_ENDPOINT", "http://server.example.com")));
        Assert.Null(Load(out _, ("AGENTMON_TARGETS", Targets), ("AGENTMON_AGENT_ID", "has space")));
        Assert.Null(Load(out _, ("AGENTMON_TARGETS", Targets), ("AGENTMON_INTERVAL_SECONDS", "0")));
        Assert.Null(Load(out _, ("AGENTMON_TARGETS", Targets), ("AGENTMON_INTERVAL_SECONDS", "abc")));
    }

    [Fact]
    public void AllowsHttpsServersAndLocalHttp()
    {
        const string Targets = "https://a.example/";
        Assert.NotNull(Load(out _, ("AGENTMON_TARGETS", Targets), ("AGENTMON_ENDPOINT", "https://agentmon.example.com")));
        Assert.NotNull(Load(out _, ("AGENTMON_TARGETS", Targets), ("AGENTMON_ENDPOINT", "http://localhost:5088")));
        Assert.Equal(TimeSpan.FromSeconds(30),
            Load(out _, ("AGENTMON_TARGETS", Targets), ("AGENTMON_INTERVAL_SECONDS", "30"))!.Interval);
    }
}
