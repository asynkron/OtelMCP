// using System.Globalization;
// using System.Net.Http.Json;
//
// namespace TraceLens.Data;
//
// public  class PrometheusRepo
// {
//     private readonly ModelRepo _modelRepo;
//     private readonly IHttpClientFactory _httpClientFactory;
//
//     public PrometheusRepo(ModelRepo modelRepo, IHttpClientFactory httpClientFactory)
//     {
//         _modelRepo = modelRepo;
//         _httpClientFactory = httpClientFactory;
//     }
//     public  async Task<PrometheusRoot> Query(string traceId, string query)
//     {
//         var model = await _modelRepo.GetModel(traceId);
//         var startDate = model!.StartSpan.StartTimeUnixNano.UnixNanosToDateTimeOffset().AddMinutes(-5).ToString("u").Replace(" ", "T");
//         var endDate = model.EndSpan.EndTimeUnixNano.UnixNanosToDateTimeOffset().AddMinutes(5).ToString("u").Replace(" ", "T");
//
//         using var client = _httpClientFactory.CreateClient();
//
//         var uri = new Uri($"http://localhost:9090/api/v1/query_range?query={Uri.EscapeDataString(query)}&start={startDate}&end={endDate}&step=1s");
//
//         Console.WriteLine(uri);
//         var res= await client.GetFromJsonAsync<PrometheusRoot>(uri);
//         return res!;
//     }
//
//
//     public  async Task<PrometheusRoot> Query(string query,long start, long end, string step)
//     {
//         var startDate = start.ToString(CultureInfo.InvariantCulture);
//         var endDate = end.ToString(CultureInfo.InvariantCulture);
//
//         using var client = _httpClientFactory.CreateClient();
//
//         var uri = new Uri($"http://localhost:9090/api/v1/query_range?query={Uri.EscapeDataString(query)}&start={startDate}&end={endDate}&step=" + step);
//
//         Console.WriteLine(uri);
//         var res= await client.GetFromJsonAsync<PrometheusRoot>(uri);
//         return res;
//     }
// }

