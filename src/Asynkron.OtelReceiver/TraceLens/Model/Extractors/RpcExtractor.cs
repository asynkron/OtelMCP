using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class RpcExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        depth = 0;
        description = null;

        //TODO: this is likely a client, not an endpoint.
        //figure out how to detect the server side
        var rpcSystem = span.GetAttribute("rpc.system");
        
        if (rpcSystem != "")
        {
            if (span.Kind == OpenTelemetry.Proto.Trace.V1.Span.Types.SpanKind.Client)
            {
                var service = span.GetAttribute("rpc.service");
                var method = span.GetAttribute("rpc.method");

                description =
                    new SpanDescription(model, span.ServiceName, service, method, ComponentKind.Service, CallKind.Sync,
                        componentStack: rpcSystem, isClient: true);
                return true;
                    
            }
            else
            {
                var service = span.GetAttribute("rpc.service");
                var method = span.GetAttribute("rpc.method");
                description =
                    new SpanDescription(model, span.ServiceName, service, method, ComponentKind.Endpoint, CallKind.Sync,
                        componentStack: rpcSystem, isClient: false);
                return true;
            }
        }

        return false;
    }
}