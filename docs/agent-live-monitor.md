# Agent-side Nest and Finesse latency monitoring

The agent dashboard displays two separately named targets: `Nest` and `Finesse`, with last measured HTTPS round-trip latency, status, and recent measurements.

## Configuration

Set the collector's `AGENTMON_TARGETS` to approved HTTPS health URLs for both systems, e.g. `https://nest.example.internal/health,https://finesse.example.internal/`. **These are placeholders, not actual hostnames.**

**Important:** The collector currently sends `target.Host` as the sample target. Until target-label mapping is implemented, the UI expects samples with target values `Nest` and `Finesse`; raw collector samples will not automatically populate the named cards. Implement a trusted backend mapping from allowlisted hostname to display label before end-to-end use.

## Integration status

The gadget currently exposes `window.AgentMon.render(samples)` for an authenticated Finesse 15 adapter to call. It does not yet poll the backend. The backend requires a shared ingestion key, which must **never** be placed in browser JavaScript. Add scoped agent read authentication, mapping of Finesse user to collector identity, and a supported gadget descriptor before deploying.

HTTP round-trip duration includes connection and server response time and does not isolate network latency. A stale sample is not evidence of a network outage. Threshold of 250ms is illustrative and must be configurable.
