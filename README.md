# Agent Mon

Agent Mon is an enterprise-oriented Cisco Finesse connectivity diagnostics project.

## Architecture
- **collector/**: .NET Windows worker that probes approved HTTPS endpoints and reports latency, reachability and errors.
- **server/**: ASP.NET Core ingestion API with an authenticated agent telemetry endpoint and a read-only gadget endpoint.
- **gadget/**: Embedded Finesse gadget UI displaying recent connectivity samples.

> This is a development foundation, not a Cisco-certified integration. Validate gadget packaging, browser policies, Finesse versions, security controls and deployment requirements in a lab before production use.

## Quick start
Requires .NET 8 SDK.

```sh
dotnet run --project server/AgentMon.Server
```

Set `AGENTMON_API_KEY` to a long random secret in the backend and collector environments. Set `AGENTMON_ENDPOINT` to the backend URL and `AGENTMON_TARGETS` to a comma-separated list of approved HTTPS health endpoints. Run:

```sh
dotnet run --project collector/AgentMon.Collector
```

The API binds to localhost by default. Configure TLS, authorization, endpoint restrictions, persistent storage and device identity before any network deployment.

## Design principles
- A browser gadget cannot run arbitrary ICMP ping. The collector measures HTTP/TLS round-trip latency, not ICMP latency or voice RTP quality.
- A missed heartbeat is not proof of network failure. Correlate Finesse state events, client reachability and collector continuity.
- Never log credentials, call content or sensitive customer data.
- In-memory telemetry is for lab testing only.
- Do not expose the sample gadget API to unauthenticated clients in production.

## Next steps
1. Add a supported Finesse gadget descriptor and version-specific Finesse API integration.
2. Add mutual TLS or device-bound authentication and scoped read access.
3. Persist telemetry, implement retention, incident correlation and audit logging.
4. Add unit/integration tests and deployment instructions.

## Support

When filing an issue, include the affected component, runtime version, and steps to reproduce. Remove API keys, customer identifiers, and internal hostnames from examples.

## License

Project code is available under the [MIT License](LICENSE).
