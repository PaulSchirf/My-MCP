# My-MCP

A Model Context Protocol (MCP) server built with C# / .NET 10. It speaks two transports:
**stdio** (the default — the MCP client launches the process and speaks JSON-RPC over its
stdin/stdout) and **Streamable HTTP** (opt-in — the same tools behind a loopback HTTP
endpoint; see [Transports](#transports)).

Its only role for now is to report the time. `src/MyMcp.Server/Tools/UTCClock.cs` exposes:

- `get_current_utc_date_time` — the current UTC time as an ISO 8601 timestamp.
- `get_current_local_date_time` — takes a time zone code (`America/New_York` (IANA) or
  `Eastern Standard Time` (Windows)) and returns that zone's local time with its UTC offset.
  A code it cannot resolve returns a guidance message rather than an error.

Asked for `EST` in October, these differ in a way worth knowing: `EST` is a legacy tzdata zone
that is permanently UTC−5, while `Eastern Standard Time` and `America/New_York` are the Eastern
*zone*, which observes daylight saving and so reports −04:00 for that part of the year.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later

## Layout

```text
My-MCP.slnx                        Solution file
.vscode/
  launch.json                      Debug configuration (F5)
  tasks.json                       build / watch tasks
  mcp.json                         Registers this server with VS Code's MCP client
  extensions.json                  Recommended extensions
src/MyMcp.Server/
  MyMcp.Server.csproj              Console app, ModelContextProtocol.AspNetCore
  Program.cs                       Transport selection + host / MCP wiring
  Tools/UTCClock.cs                The [McpServerTool] methods
  appsettings*.json                Logging / transport configuration
```

## Build and run

In the default stdio mode the client owns the process, so there is no port to listen on and
nothing to keep running in the background: build the assembly first (or leave the `watch`
task running), and the client starts it on demand. In HTTP mode you run the server yourself
instead; see [Transports](#transports).

```powershell
dotnet build My-MCP.slnx
```

Press <kbd>F5</kbd> to launch the same assembly under the debugger when you want to step
through tool code.

## Connect an MCP client

VS Code is already wired up through `.vscode/mcp.json`, so just enable the `my-mcp` server
from the MCP servers list in Copilot Chat. The equivalent configuration is:

```json
{
  "servers": {
    "my-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["${workspaceFolder}/src/MyMcp.Server/bin/Debug/net10.0/MyMcp.Server.dll"],
      "env": { "DOTNET_ENVIRONMENT": "Development" }
    }
  }
}
```

Then ask something like `What is the current UTC time?` — the client should call the
`get_current_utc_date_time` tool. After editing tool code, rebuild and restart the server
(`MCP: List Servers` → Restart) so the client picks up the new assembly.

To reach the same tools over HTTP, start the server in HTTP mode (see
[Transports](#transports)) and register the URL instead:

```json
{
  "servers": {
    "my-mcp-http": {
      "type": "http",
      "url": "http://127.0.0.1:5222/"
    }
  }
}
```

The two entries can coexist: they are separate clients against separate processes.

To smoke-test without a client, send the JSON-RPC sequence `initialize` →
`notifications/initialized` → `tools/list` (or `tools/call`) over stdin and read the
responses from stdout. Keep stdin open until you have read them: closing it tears the server
down, and responses still in flight are dropped.

Over HTTP, POST the same JSON-RPC messages to the endpoint with
`Accept: application/json, text/event-stream` and `MCP-Protocol-Version: 2025-11-25`.
Because the transport is stateless, no `initialize` handshake is needed, and responses arrive
as `text/event-stream`:

```powershell
$headers = @{ 'Accept' = 'application/json, text/event-stream'; 'MCP-Protocol-Version' = '2025-11-25' }
Invoke-WebRequest -Method Post -Uri 'http://127.0.0.1:5222/' -Headers $headers -ContentType 'application/json' `
  -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' -UseBasicParsing | Select-Object -ExpandProperty Content
```

## Adding a tool

Create a class in `Tools/` and decorate the method. Discovery is automatic
(`WithToolsFromAssembly()`), so no registration is required — but the class itself must be
marked `[McpServerToolType]`, otherwise its methods are ignored and `tools/list` returns nothing:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
internal class MyTools
{
    [McpServerTool]
    [Description("Explains what this tool does — the client sees this text.")]
    public string DoSomething(
        [Description("Describe each parameter: the client sees this too.")] string input)
    {
        return input;
    }
}
```

Tools can also take injected services by declaring them as constructor parameters.

## Notes

### Transports

The transport is chosen at startup in `Program.cs`, before any host exists — stdio mode must
never start Kestrel, and HTTP mode must not treat stdout as a protocol channel. Precedence:

1. `--http` on the command line.
2. `Mcp:Transport` set to `http` (`appsettings.json`, an environment variable such as
   `Mcp__Transport=http`, user secrets).
3. Otherwise stdio.

**stdio is the default** and the right fit for a server whose only client is the IDE: the
client launches the process and talks to it over stdin/stdout, so there is no port, no URL, no
development certificate, and no "is it running?" question — one process per client, started on
demand, with the client stopping it when the session ends.

**HTTP is opt-in** and turns this process into a server you run yourself:

```powershell
dotnet src/MyMcp.Server/bin/Debug/net10.0/MyMcp.Server.dll --http
```

It listens on `http://127.0.0.1:5222/`, mapping the MCP endpoint at the root through
`app.MapMcp()`; override the address with `Mcp:Http:Url`. The
`Launch My-MCP (Streamable HTTP)` debug configuration does the same under the debugger.

Three consequences worth knowing before you add code:

- **stdout is the protocol channel in stdio mode.** A single stray line written to stdout
  corrupts the JSON-RPC stream, which is why the stdio branch clears the default logging
  providers and sends console logs to stderr via `LogToStandardErrorThreshold`. Keep that
  split. HTTP mode has no such constraint and keeps the default console logging.
- **The content root is pinned to `AppContext.BaseDirectory`** so `appsettings.json` resolves
  next to the assembly no matter which directory the client launches the process from.
- **HTTP is stateless and unauthenticated.** `Stateless` is the SDK default as of the
  2026-07-28 protocol revision and is set explicitly here: no session IDs, so requests need no
  `initialize` handshake and any instance can serve them. There is no authentication, which is
  exactly why the default URL is loopback-only — do not bind it to a wider address before
  adding an auth scheme. Legacy SSE is deliberately left off; the SDK throws at startup if you
  enable it while stateless.

### Publishing

The project builds framework-dependent by default, which keeps local builds and debugging simple.
Because it now references the ASP.NET Core framework (required for the HTTP transport), the
ASP.NET Core shared framework is needed at run time — present wherever the .NET SDK is
installed, but a new prerequisite if you publish elsewhere, and relevant again if you later
switch to self-contained or trimmed publishing.
Publish for a specific platform on demand — add `--self-contained` and `-p:PublishSingleFile=true`
if you need a standalone, runtime-free executable:

```powershell
dotnet publish src/MyMcp.Server -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

If you change the output location, update the assembly path in `.vscode/mcp.json` to match.

## More information

- [MCP specification](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)
- [ModelContextProtocol on NuGet](https://www.nuget.org/packages/ModelContextProtocol)
- [Transport comparison (stdio vs. Streamable HTTP)](https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html)
