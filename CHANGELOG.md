# Changelog

## 1.0.0

- **Dashboard connects by itself:** open the server's address, paste the read-only key, pick an agent, and it updates every 5 seconds. The key is kept only for that browser tab. Finesse integrations can still call `window.AgentMon.render(samples)`.
- **Ready-to-run downloads** for Windows, macOS and Linux on each release, with SHA-256 checksums. No .NET install needed.
- **Docker image** for the server.
- Collector tests all targets at the same time, so one slow system doesn't delay the others.
- New setting `AGENTMON_INTERVAL_SECONDS` (1 to 300; default 5).
- Collector reports `DNS_ERROR` and `TLS_ERROR` separately from other connection failures, and leaves the round-trip time empty when a system doesn't answer.
- Collector and server explain what to fix when a setting is wrong, instead of crashing. The collector says when delivery to the server fails and when it recovers, instead of repeating the error.
- Server: `GET /healthz`, `GET /api/v1/agents`, request size limit, limit of 5,000 agents, stricter checks on agent IDs and error text, security headers, and quieter logs.
- Automated tests for the server and collector, run on Windows, macOS and Linux for every pull request, plus a check that the release download starts and serves the dashboard.

## 0.1.0

- Lab prototype: collector, in-memory server and a standalone dashboard page.
