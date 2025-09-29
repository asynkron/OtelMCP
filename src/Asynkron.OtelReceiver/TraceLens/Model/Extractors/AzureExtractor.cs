using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class AzureExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        description = null;
        (var url, depth) = span.GetParentTag("http.url");
        if (url.Contains("services.visualstudio.com"))
        {
            description = new SpanDescription(model,"Azure", "ApplicationInsights", "", ComponentKind.Service, CallKind.Sync,
                componentStack: "Azure ApplicationInsights");
            return true;
        }

        if (url.Contains("opinsights.azure.com"))
        {
            description =
                new SpanDescription(model,"Azure", "Operations Management", "", ComponentKind.Service, CallKind.Sync,
                    componentStack: "Azure Operations Management");
            return true;
        }

        return false;
    }
}