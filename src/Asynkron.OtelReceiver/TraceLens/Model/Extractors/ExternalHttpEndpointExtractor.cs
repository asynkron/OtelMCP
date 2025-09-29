using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class ExternalHttpEndpointExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        depth = 0;
        description = null;
        var httpMethod = span.GetAttribute("http.method");
        if (httpMethod == "") return false;
        var peerService = span.GetAttribute("peer.service");
        if (peerService == "") return false;
        description = new SpanDescription(model,"HTTP", peerService, "", ComponentKind.Service, CallKind.Sync,
            componentStack: "HTTP");
        return true;
    }
}