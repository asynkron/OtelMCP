using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class HttpEndpointExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        depth = 0;
        description = null;
        var httpRoute = span.GetAttribute("http.route");
        if (httpRoute != "")
        {
            //http.request.method
            var httpMethod = span.GetAttribute("http.method");
            var httpRequestMethod = span.GetAttribute("http.request.method");
            
            description = new SpanDescription(model,span.ServiceName, httpRoute,
                $"HTTP {httpMethod.ToUpperInvariant()}{httpRequestMethod.ToUpperInvariant()}",
                ComponentKind.Endpoint, CallKind.Sync, componentStack: "ASP.NET Core");
            return true;
        }

        var httpUrl = span.GetAttribute("http.url");
        if (httpUrl != "")
        {
            var httpHost = span.GetAttribute("http.host");
            var httpMethod = span.GetAttribute("http.method");
            var httpRequestMethod = span.GetAttribute("http.request.method");
            description = new SpanDescription(model, span.ServiceName, httpHost,
                $"HTTP {httpMethod.ToUpperInvariant()}{httpRequestMethod.ToUpperInvariant()}",
                ComponentKind.Endpoint, CallKind.Sync, "Unknown HTTP Server");
            return true;
        }

        return false;
    }
}