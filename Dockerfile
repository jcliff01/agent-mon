# Agent Mon server, with the dashboard at /dashboard.
#   docker build -t agent-mon .
#   docker run -p 5088:5088 -e AGENTMON_API_KEY=... -e AGENTMON_READ_KEY=... agent-mon
# Put it behind an HTTPS reverse proxy before exposing it beyond one machine.
# Health check for orchestrators: GET /healthz (no key needed).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish server/AgentMon.Server -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV AGENTMON_URLS=http://0.0.0.0:5088
EXPOSE 5088
USER app
ENTRYPOINT ["dotnet", "AgentMon.Server.dll"]
