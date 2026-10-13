# My-MCP

A Model Context Protocol (MCP) server built with C# / .NET 10, exposed over the
**stdio** transport — the MCP client launches the process and speaks JSON-RPC over its
stdin/stdout.

Its only role for now is to report the current Coordinated Universal Time (UTC):
`src/MyMcp.Server/Tools/UTCClock.cs` exposes a single `get_current_utc_date_time` tool that
returns an ISO 8601 timestamp.

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
  MyMcp.Server.csproj              Console app, ModelContextProtocol
  Program.cs                       Host + MCP wiring (stdio)
  Tools/UTCClock.cs                The get_current_utc_date_time [McpServerTool]
  appsettings*.json                Logging / configuration
```

## Build and run

The client owns the process, so there is no port to listen on and nothing to keep running
in the background: build the assembly first (or leave the `watch` task running), and the
client starts it on demand.

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

To smoke-test without a client, send the JSON-RPC sequence `initialize` →
`notifications/initialized` → `tools/list` (or `tools/call`) over stdin and read the
responses from stdout. Keep stdin open until you have read them: closing it tears the server
down, and responses still in flight are dropped.

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

### Transport

stdio is the right fit for a server whose only client is the IDE: the client launches the
process and talks to it over stdin/stdout, so there is no port, no URL, no development
certificate, and no "is it running?" question — one process per client, started on demand,
with the client stopping it when the session ends.

Two consequences worth knowing before you add code:

- **stdout is the protocol channel.** A single stray line written to stdout corrupts the
  JSON-RPC stream, which is why `Program.cs` clears the default logging providers and sends
  console logs to stderr via `LogToStandardErrorThreshold`. Keep that split.
- **The content root is pinned to `AppContext.BaseDirectory`** so `appsettings.json` resolves
  next to the assembly no matter which directory the client launches the process from.

To host this over HTTP instead (remote clients, multiple consumers, or a long-running
service), switch the package back to `ModelContextProtocol.AspNetCore`, build with
`WebApplication.CreateBuilder` + `WithHttpTransport()` + `app.MapMcp()`, and point clients at
the URL.

### Publishing

The project builds framework-dependent by default, which keeps local builds and debugging simple.
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
