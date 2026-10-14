using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools that expose current clock values: Coordinated Universal Time (UTC)
/// and the local time of a requested time zone.
/// </summary>
[McpServerToolType]
internal class UTCClock
{
    private const string RoundTripFormat = "O";

    private const string InvalidTimeZoneMessage = "Unknown time zone code. Supply an IANA code (for example \"America/Los_Angeles\") or a Windows code (for example \"Pacific Standard Time\").";

    [McpServerTool]
    [Description("Gets the current Coordinated Universal Time (UTC) date and time as an ISO 8601 timestamp.")]
    public string GetCurrentUtcDateTime()
    {
        return DateTimeOffset.UtcNow.UtcDateTime.ToString(RoundTripFormat, CultureInfo.InvariantCulture);
    }

    [McpServerTool]
    [Description("Gets the current local date and time for the requested time zone code, returned as an ISO 8601 timestamp with its UTC offset.")]
    public string GetCurrentLocalDateTime(
        [Description("The time zone code, for example \"America/Los_Angeles\" (IANA) or \"Pacific Standard Time\" (Windows).")]
        string timeZoneCode)
    {
        if (timeZoneCode == "")
        {
            return InvalidTimeZoneMessage;
        }

        try
        {
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneCode);
            DateTimeOffset localDateTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);

            return localDateTime.ToString(RoundTripFormat, CultureInfo.InvariantCulture);
        }
        catch (TimeZoneNotFoundException)
        {
            return InvalidTimeZoneMessage;
        }
        catch (InvalidTimeZoneException)
        {
            return InvalidTimeZoneMessage;
        }
    }
}
