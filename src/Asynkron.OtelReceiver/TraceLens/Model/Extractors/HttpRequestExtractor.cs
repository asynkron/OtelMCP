using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

public class HttpRequestExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        depth = 0;
        description = null;
        var httpMethod = span.GetAttribute("http.request.method");
        var urlFull = span.GetAttribute("url.full");
        
        if (httpMethod != "")
        {
            description = new SpanDescription(model, span.ServiceName, "HTTP Client",
                $"HTTP {httpMethod.ToUpperInvariant()} {urlFull}",
                ComponentKind.Service, CallKind.Sync, componentStack: "",isClient:true);
            return true;
        }

        return false;
    }
}