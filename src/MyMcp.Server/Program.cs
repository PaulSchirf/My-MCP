using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// appsettings.json lives next to the assembly; MCP clients launch this process
// from their own working directory, so pin the content root instead of assuming one.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Register the MCP server over the stdio transport: the client launches this
// process itself and speaks JSON-RPC over its stdin/stdout. There is no URL to
// listen on, and each client gets its own process. WithToolsFromAssembly picks up
// every tool class decorated with [McpServerToolType].
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// stdout is the protocol channel, so every log record has to go to stderr
// instead. Clearing the default providers first guarantees nothing else keeps
// writing to stdout (including the default console provider).
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

await builder.Build().RunAsync();
