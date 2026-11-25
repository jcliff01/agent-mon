# Agent Mon

Agent Mon checks whether a contact-center agent's computer can reach the systems it depends on, such as Cisco Finesse, and how quickly. A small program on the agent's PC (the **collector**) tests each system every 5 seconds and sends the results to a **server**. A web page (the **gadget**) shows the results to the agent.

> **Status: lab prototype.** It is not a Cisco-certified integration and has not been tested with a live Finesse server. Use it in a test lab, not with real agents yet.

## What it measures, and what it doesn't

- It measures how long an HTTPS request to each system takes, and whether the system answers.
- It does **not** measure ping (ICMP) or call audio quality. A web page cannot send pings.
- A missing measurement means the collector stopped reporting. It doesn't prove the network is down.

## What you need

- A Windows, macOS or Linux computer.
- The **.NET 8 SDK** (free, from Microsoft). Install it as shown below.
- The web address of each system you want to test, for example your Finesse server. Each address must start with `https://`.

## Step 1: Install the .NET 8 SDK

**Windows:** open PowerShell and run:

```powershell
winget install Microsoft.DotNet.SDK.8
```

**macOS:** download and run the macOS installer from [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0). Choose **Arm64** for Apple silicon Macs (M1 or later) or **x64** for Intel Macs.

**Linux (Ubuntu 22.04 or later):**

```sh
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

Close the terminal and open a new one. Check that it worked: `dotnet --version` should print a number starting with 8.

## Step 2: Get the code

Either click the green **Code** button on this page, choose **Download ZIP** and unzip it, or run:

```sh
git clone https://github.com/jcliff01/agent-mon.git
```

Then open a terminal in the `agent-mon` folder.

## Step 3: Make a secret key

The collector and the server share a secret key, so nobody else can send fake results. Make a long random one:

**Windows (PowerShell):**

```powershell
$env:AGENTMON_API_KEY = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
```

**macOS or Linux:**

```sh
export AGENTMON_API_KEY=$(openssl rand -hex 32)
```

The key only lasts while this terminal window is open. Use the same key in both windows in the next step: print it with `echo $env:AGENTMON_API_KEY` (Windows) or `echo $AGENTMON_API_KEY` (macOS/Linux), and set it in the second window the same way.

## Step 4: Start the server

In the first terminal:

```sh
dotnet run --project server/AgentMon.Server
```

It listens on `http://127.0.0.1:5088`, which only this computer can reach. Leave it running.

## Step 5: Start the collector

Open a second terminal in the same folder, set the same `AGENTMON_API_KEY` (step 3), then tell the collector what to test. Give each address a name; the name is what the dashboard shows.

**Windows (PowerShell):**

```powershell
$env:AGENTMON_TARGETS = "Finesse=https://finesse.example.com/,Nest=https://nest.example.com/health"
dotnet run --project collector/AgentMon.Collector
```

**macOS or Linux:**

```sh
export AGENTMON_TARGETS="Finesse=https://finesse.example.com/,Nest=https://nest.example.com/health"
dotnet run --project collector/AgentMon.Collector
```

Replace the example addresses with your own. Press **Ctrl+C** to stop either program.

## Step 6: See the results

Ask the server for the latest results. Replace `MYPC` with the collector's computer name (it is the default agent ID).

**Windows (PowerShell):**

```powershell
Invoke-RestMethod http://127.0.0.1:5088/api/v1/agents/MYPC/samples -Headers @{ "X-AgentMon-Key" = $env:AGENTMON_API_KEY }
```

**macOS or Linux:**

```sh
curl -H "X-AgentMon-Key: $AGENTMON_API_KEY" http://127.0.0.1:5088/api/v1/agents/MYPC/samples
```

The page in `gadget/` shows the same results once something feeds them to it (see [docs/agent-live-monitor.md](docs/agent-live-monitor.md)). It does not fetch them by itself yet.

## Settings

| Setting | Used by | What it does |
| --- | --- | --- |
| `AGENTMON_API_KEY` | server and collector | Shared secret, at least 32 characters. Required. |
| `AGENTMON_TARGETS` | collector | Comma-separated `Name=https://address` pairs. Required. |
| `AGENTMON_ENDPOINT` | collector | Server address. Default `http://127.0.0.1:5088`. Must be HTTPS unless it is this computer. |
| `AGENTMON_AGENT_ID` | collector | Name for this PC in the results. Default: the computer name. |
| `AGENTMON_READ_KEY` | server | Optional second secret that can only read results, for whatever shows the dashboard. |
| `AGENTMON_URLS` | server | Address the server listens on. Default `http://127.0.0.1:5088`. |

## Troubleshooting

| Message | What to do |
| --- | --- |
| `Set AGENTMON_API_KEY to a random secret of at least 32 characters` | Do step 3 in this terminal window. |
| `Set AGENTMON_TARGETS to approved HTTPS URLs` | Each address must start with `https://`. |
| `Telemetry delivery failed` | The server isn't running, or the two windows have different keys. |
| `dotnet: command not found` or not recognized | Open a new terminal after installing .NET, or reinstall it (step 1). |
| `401 Unauthorized` when reading results | The key in the request doesn't match the server's key. |

## Before using it beyond a test lab

The server keeps results in memory only and forgets them when it stops. Before putting it on a network, add HTTPS, per-device sign-in, permanent storage and a supported Finesse gadget package. [docs/finesse-15.md](docs/finesse-15.md) lists the integration steps. Never log passwords, call content or customer data, and remove keys and internal host names from anything you share.

## License

[MIT](LICENSE).
