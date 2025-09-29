// using System.Text.Json.Serialization;
//
// namespace TraceLens.Data;
//
// // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
// // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
// public record Data(
//     [property: JsonPropertyName("resultType")] string ResultType,
//     [property: JsonPropertyName("result")] IReadOnlyList<Result> Result
// );
//
// public record Result(
//     [property: JsonPropertyName("metric")] IDictionary<string,string> Metric,
//     [property: JsonPropertyName("values")] IReadOnlyList<List<double>> Values
// );
//
// public record PrometheusRoot(
//     [property: JsonPropertyName("status")] string Status,
//     [property: JsonPropertyName("data")] Data Data
// );

