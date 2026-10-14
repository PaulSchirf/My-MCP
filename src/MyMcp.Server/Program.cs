using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// The transport has to be chosen before any host exists: stdio mode must never
// start Kestrel, and HTTP mode must not treat stdout as a protocol channel. The
// setting is read from the same sources the host itself will use.
const string TransportSettingKey = "Mcp:Transport";
const string HttpUrlSettingKey = "Mcp:Http:Url";
const string HttpTransportName = "http";
const string HttpCommandLineSwitch = "--http";
const string DefaultHttpUrl = "http://127.0.0.1:5222";

// appsettings.json lives next to the assembly; MCP clients launch this process
// from their own working directory, so pin the content root instead of assuming one.
string environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Production;

IConfigurationRoot startupConfiguration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

bool useHttpTransport = args.Contains(HttpCommandLineSwitch)
    || string.Equals(startupConfiguration[TransportSettingKey], HttpTransportName, StringComparison.OrdinalIgnoreCase);

if (useHttpTransport)
{
    // Streamable HTTP mode: this process is the server, so it binds a port and
    // stays up. WithToolsFromAssembly picks up every tool class decorated with
    // [McpServerToolType].
    WebApplicationBuilder webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

    // Loopback by default: this endpoint has no authentication, so binding it to
    // anything wider would expose every tool to the network.
    webBuilder.WebHost.UseUrls(startupConfiguration[HttpUrlSettingKey] ?? DefaultHttpUrl);

    // Stateless is already the default as of the 2026-07-28 protocol revision; it
    // is set explicitly because this server keeps nothing between requests. Note
    // that legacy SSE cannot be enabled alongside it (it throws at startup).
    webBuilder.Services
        .AddMcpServer()
        .WithHttpTransport(options => options.Stateless = true)
        .WithToolsFromAssembly();

    WebApplication webApplication = webBuilder.Build();
    webApplication.MapMcp();

    await webApplication.RunAsync();
}
else
{
    // stdio mode: the client launches this process itself and speaks JSON-RPC over
    // its stdin/stdout. There is no URL to listen on, and each client gets its own
    // process.
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

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
}
