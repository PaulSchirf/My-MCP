using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools that expose the current Coordinated Universal Time (UTC).
/// </summary>
[McpServerToolType]
internal class UTCClock
{
    private const string RoundTripFormat = "O";

    [McpServerTool]
    [Description("Gets the current Coordinated Universal Time (UTC) date and time as an ISO 8601 timestamp.")]
    public string GetCurrentUtcDateTime()
    {
        return DateTimeOffset.UtcNow.UtcDateTime.ToString(RoundTripFormat, CultureInfo.InvariantCulture);
    }
}
