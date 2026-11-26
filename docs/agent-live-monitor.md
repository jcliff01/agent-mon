# Agent-side Nest and Finesse latency monitoring

The agent dashboard shows a card per monitored system (by default `Nest` and `Finesse`) with the last measured HTTPS round-trip time, a status, and the recent measurements.

## Configuration

Name each target in the collector's `AGENTMON_TARGETS`, so the dashboard shows that name:

```text
AGENTMON_TARGETS=Nest=https://nest.example.internal/health,Finesse=https://finesse.example.internal/
```

These host names are placeholders. A URL without a name (`https://finesse.example.internal/`) is shown by its host name. The dashboard shows Nest and Finesse cards while it waits for data, then one card for each name the collector reports.

## Integration status

Opened from the Agent Mon server (`/dashboard/`), the page asks for the read-only key and polls the server every 5 seconds. Inside Finesse, the page exposes `window.AgentMon.render(samples)` for an authenticated Finesse 15 adapter to call; the first call hides the key form and stops polling. Whatever feeds it should use the read-only key (`AGENTMON_READ_KEY`), never the collector's write key (`AGENTMON_API_KEY`), and never put either key in browser JavaScript. Before deploying, add mapping from the Finesse user to the collector's identity and a supported gadget descriptor.

HTTP round-trip time includes connection and server response time, so it does not isolate network latency. A stale sample is not evidence of a network outage. The 250 ms "Slow" threshold is illustrative.
