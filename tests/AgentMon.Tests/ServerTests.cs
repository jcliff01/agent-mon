using AgentMon.Server;
using Xunit;

namespace AgentMon.Tests;

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class SampleStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 3, 0, 0, TimeSpan.Zero);

    private static ProbeSample Sample(string agent = "PC-1", string target = "Finesse", double? latency = 42,
        DateTimeOffset? at = null, string? error = null) =>
        new(agent, target, at ?? Now, true, latency, error);

    [Fact]
    public void AcceptsAGoodSample()
    {
        Assert.Null(new SampleStore(new FixedClock(Now)).Validate(Sample()));
    }

    [Fact]
    public void RejectsBadSamples()
    {
        var store = new SampleStore(new FixedClock(Now));
        Assert.NotNull(store.Validate(null));
        Assert.NotNull(store.Validate(Sample(agent: "")));
        Assert.NotNull(store.Validate(Sample(agent: "has space")));
        Assert.NotNull(store.Validate(Sample(agent: "../etc")));
        Assert.NotNull(store.Validate(Sample(agent: new string('a', 129))));
        Assert.NotNull(store.Validate(Sample(target: "")));
        Assert.NotNull(store.Validate(Sample(target: "bad\nname")));
        Assert.NotNull(store.Validate(Sample(latency: -1)));
        Assert.NotNull(store.Validate(Sample(latency: double.NaN)));
        Assert.NotNull(store.Validate(Sample(latency: 120_001)));
        Assert.NotNull(store.Validate(Sample(at: Now.AddDays(-2))));
        Assert.NotNull(store.Validate(Sample(at: Now.AddMinutes(10))));
        Assert.NotNull(store.Validate(Sample(error: new string('e', 201))));
    }

    [Fact]
    public void AllowsMissingLatencyForFailures()
    {
        Assert.Null(new SampleStore(new FixedClock(Now)).Validate(Sample(latency: null, error: "TIMEOUT")));
    }

    [Fact]
    public void KeepsOnlyTheNewestSamplesPerAgent()
    {
        var store = new SampleStore(new FixedClock(Now), samplesPerAgent: 3);
        for (int i = 0; i < 5; i++)
            store.Add(Sample(latency: i));
        var kept = store.Get("PC-1");
        Assert.Equal(3, kept.Length);
        Assert.Equal(new double?[] { 2, 3, 4 }, kept.Select(s => s.LatencyMs).ToArray());
    }

    [Fact]
    public void RefusesNewAgentsPastTheLimit()
    {
        var store = new SampleStore(new FixedClock(Now), maxAgents: 2);
        Assert.True(store.Add(Sample(agent: "A")));
        Assert.True(store.Add(Sample(agent: "B")));
        Assert.False(store.Add(Sample(agent: "C")));
        Assert.True(store.Add(Sample(agent: "A")));   // existing agents still report
    }

    [Fact]
    public void AgentIdsIgnoreCase()
    {
        var store = new SampleStore(new FixedClock(Now));
        store.Add(Sample(agent: "PC-1"));
        Assert.Single(store.Get("pc-1"));
    }

    [Fact]
    public void ListsAgentsWithTheirLatestSample()
    {
        var store = new SampleStore(new FixedClock(Now));
        store.Add(Sample(agent: "B", at: Now.AddMinutes(-1)));
        store.Add(Sample(agent: "A", at: Now.AddMinutes(-3)));
        store.Add(Sample(agent: "A", at: Now.AddMinutes(-2)));
        var agents = store.Agents();
        Assert.Equal(new[] { "A", "B" }, agents.Select(a => a.AgentId).ToArray());
        Assert.Equal(Now.AddMinutes(-2), agents[0].LastSeen);
        Assert.Equal(2, agents[0].Samples);
    }
}

public class KeysTests
{
    private static readonly string Write = new('w', 32);
    private static readonly string Read = new('r', 32);

    [Fact]
    public void RequiresALongWriteKey()
    {
        Assert.Null(Keys.Load(null, null, out var problem));
        Assert.NotNull(problem);
        Assert.Null(Keys.Load("short", null, out _));
    }

    [Fact]
    public void ReadKeyMustDifferFromWriteKey()
    {
        Assert.Null(Keys.Load(Write, Write, out _));
        Assert.Null(Keys.Load(Write, "short", out _));
    }

    [Fact]
    public void WithoutAReadKeyTheWriteKeyReads()
    {
        var keys = Keys.Load(Write, null, out _)!;
        Assert.True(keys.CanWrite(Write));
        Assert.True(keys.CanRead(Write));
        Assert.False(keys.CanRead(Read));
        Assert.False(keys.CanRead(null));
        Assert.False(keys.CanRead(""));
    }

    [Fact]
    public void TheReadKeyCanOnlyRead()
    {
        var keys = Keys.Load(Write, Read, out _)!;
        Assert.True(keys.CanRead(Read));
        Assert.False(keys.CanWrite(Read));
        Assert.True(keys.CanWrite(Write));
        Assert.False(keys.CanRead(Write));
    }
}
