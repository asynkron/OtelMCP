using System.Globalization;

namespace TraceLens.Model;

public enum LogEntryLevel
{
    Diagnostics,
    Span,
    Event,
    Debug,
    Info,
    Warning,
    Error

    //special
}

public record LogEntry(ulong TimeUnixNano, string LogLevel, string Body, Dictionary<string, object?> Attributes,
    int SortOrder = 2)
{
    public LogEntryLevel Level { get; } = GetLevel(LogLevel, Body);

    public string Id { get; } = "Log" + Guid.NewGuid().ToString("N");

    public Span Span { get; internal set; } = default!;

    private static LogEntryLevel GetLevel(string level, string body)
    {
        var ll = level.ToLowerInvariant();
        if (body == "exception") return LogEntryLevel.Error;
        if (ll.StartsWith("inf")) return LogEntryLevel.Info;
        if (ll.StartsWith("war")) return LogEntryLevel.Warning;
        if (ll.StartsWith("err")) return LogEntryLevel.Error;
        if (ll.StartsWith("deb")) return LogEntryLevel.Debug;
        if (ll.StartsWith("eve")) return LogEntryLevel.Event;
        if (ll.StartsWith("diag")) return LogEntryLevel.Diagnostics;
        if (ll.StartsWith("tag")) return LogEntryLevel.Span;
        return LogEntryLevel.Debug;
    }

    public string Format()
    {
        var formatBody = Body;

        foreach (var attribute in Attributes)
        {
            var value = attribute.Value;
            var str = Convert.ToString(value, CultureInfo.InvariantCulture);

            formatBody = formatBody.Replace($"{{{attribute.Key}}}", str);
        }

        return formatBody;
    }

    public string FormatWithAttributes()
    {
        var formatBody = Body;

        var allAttribs = new Dictionary<string, object?>(Attributes);
        foreach (var attribute in Attributes)
        {
            var value = attribute.Value;
            var str = value?.ToString() ?? "null";

            var prev = formatBody;
            formatBody = formatBody.Replace($"{{{attribute.Key}}}", str);
            if (prev != formatBody) allAttribs.Remove(attribute.Key);
        }

        foreach (var attribute in allAttribs) formatBody += $"\n{attribute.Key}: {attribute.Value}";

        return formatBody;
    }
}