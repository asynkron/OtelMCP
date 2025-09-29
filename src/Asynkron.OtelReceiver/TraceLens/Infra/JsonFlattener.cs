using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TraceLens.Infra;

public static class JsonFlattener
{
    public static List<JsonData> Flatten(string json, ILogger? logger = null)
    {
        var data = new List<JsonData>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            Recurse(doc.RootElement, "", 0, data);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error parsing {Json}: {ExceptionMessage}", json, ex.Message);
        }

        return data;
    }

    private static void Recurse(JsonElement e, string path, int depth, List<JsonData> data)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Undefined:
                break;
            case JsonValueKind.Object:
                if (depth > 0) path += ".";
                foreach (var prop in e.EnumerateObject()) Recurse(prop.Value, path + prop.Name, depth + 1, data);
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in e.EnumerateArray())
                {
                    var arrayPath = string.IsNullOrEmpty(path) ? i.ToString() : $"{path}.{i}";
                    Recurse(item, arrayPath, depth + 1, data);
                    i++;
                }

                if (i == 0) data.Add(new JsonData(path, "[]"));
                break;
            case JsonValueKind.String:
                data.Add(new JsonData(path, e.GetString()!));
                break;
            case JsonValueKind.Number:
                data.Add(new JsonData(path, e.GetDouble()));
                break;
            case JsonValueKind.True:
                data.Add(new JsonData(path, true));
                break;
            case JsonValueKind.False:
                data.Add(new JsonData(path, false));
                break;
            case JsonValueKind.Null:
                data.Add(new JsonData(path, null));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public record JsonData(string Path, object? Value);
}