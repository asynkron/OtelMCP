using System.Globalization;
using Google.Protobuf;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Trace.V1;
using TraceLens.Model;

namespace TraceLens.Infra;

public static class Extensions
{
    public static string GetServiceName(this ResourceSpans resourceSpan)
    {
        return resourceSpan.Resource.Attributes.First(y => y.Key == "service.name").Value.StringValue;
    }

    public static object ToValue(this AnyValue value)
    {
        return value.ValueCase switch
        {
            AnyValue.ValueOneofCase.None => "",
            AnyValue.ValueOneofCase.StringValue => new AttributeString(value.StringValue, false),
            AnyValue.ValueOneofCase.IntValue => value.IntValue,
            AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue,
            AnyValue.ValueOneofCase.BoolValue => value.BoolValue,
            AnyValue.ValueOneofCase.ArrayValue => value.ArrayValue,
            AnyValue.ValueOneofCase.KvlistValue => value.KvlistValue,
            AnyValue.ValueOneofCase.BytesValue => value.BytesValue,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public static string ToStringValue(this AnyValue value)
    {
        return value.ValueCase switch
        {
            AnyValue.ValueOneofCase.None => "",
            AnyValue.ValueOneofCase.StringValue => value.StringValue,
            AnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.BoolValue => value.BoolValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.ArrayValue => value.ArrayValue.ToString(),
            AnyValue.ValueOneofCase.KvlistValue => value.KvlistValue.ToString(),
            AnyValue.ValueOneofCase.BytesValue => value.BytesValue.ToString(),
            _ => throw new ArgumentOutOfRangeException()
        } ?? string.Empty;
    }


    public static string ToHex(this ByteString bytes)
    {
        var hex = BitConverter.ToString(bytes.ToByteArray()).Replace("-", "");
        return hex;
    }
    
    public static ByteString FromHex(this string self)
    {
        var bytes = HexStringToByteArray(self);
        return ByteString.CopyFrom(bytes);
    }
    static byte[] HexStringToByteArray(string hex)
    {
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have an even number of characters");

        byte[] byteArray = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2)
        {
            byteArray[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return byteArray;
    }

    public static DateTimeOffset ToDateTimeOffset(this ulong t)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds((long)(t / 1_000_000));
    }

    public static TimeSpan ToTimeSpan(this ulong nanos)
    {
        return TimeSpan.FromTicks((long)(nanos / 100));
    }
}