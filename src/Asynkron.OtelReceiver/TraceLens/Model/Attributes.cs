using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Hocon;
using Hocon.Json;

namespace TraceLens.Model;

public class AttributeString
{
    private static readonly UTF8Encoding Utf8 = new(true, true);
    private IList<string> _steps = Array.Empty<string>();

    public AttributeString(string value, bool magic)
    {
        Value = value;
        Magic = magic;
        Evaluate();
    }

    public bool Magic { get; }

    public string? ProtoAny { get; set; }

    public string? Decoded { get; set; }

    public byte[]? Bytes { get; set; }

    public string Value { get; }

    public bool IsJson => Json != null;
    public bool IsBytes => Bytes != null;

    public bool IsDecoded => Decoded != null;

    public string? Json { get; set; }

    private void Evaluate()
    {
        if (Value.Length < 10) return;
        _steps = new List<string>();
        ParseJson(Value);
        ParseBase64();
        //ParseProtoAny();
        if (!IsBytes) return;
        Decoded = DecodeBytes();
        if (Decoded != null) ParseJson(Decoded);
    }

    private void ParseProtoAny()
    {
        try
        {
            var x = new Any
            {
                TypeUrl = "unknown",
                Value = ByteString.CopyFrom(Bytes)
            };

            ProtoAny = x.ToString();
            _steps.Add("Protobuf Any");
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private string? DecodeBytes()
    {
        try
        {
            var res = Utf8.GetString(Bytes!);
            _steps.Add("Decoded UTF8");
            return res;
        }
        catch (Exception)
        {
        }

        return null;
    }

    private void ParseBase64()
    {
        var value = Value;
        try
        {
            Bytes = Convert.FromBase64String(value);
            _steps.Add("Base64");
        }
        catch
        {
        }
    }

    public override string ToString()
    {
        if (ProtoAny != null) return ProtoAny;


        if (IsJson) return Json!;
        if (IsDecoded) return Decoded!;
        return Value;
    }

    public IList<string> GetFormat()
    {
        return _steps;
    }


    private void ParseJson(string value)
    {
        var json = value;
        json = json.Trim();
        if (!(json.EndsWith("}") || json.EndsWith("]")))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                Json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                _steps.Add("JSON");
            }

            return;
        }
        catch
        {
        }

        try
        {
            var root = HoconParser.Parse(json).ToJToken();
            var maybe = root!.ToString();
            var doc = JsonDocument.Parse(maybe);
            if (doc.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                Json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                _steps.Add("JSON-ish");
            }
        }
        catch
        {
        }
    }
}