using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class OrleansExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        depth = 0;
        description = null;

        var orleans = span.GetAttribute("rpc.system");
        if (orleans == "orleans")
        {
            var service = span.GetAttribute("rpc.service");
            var method = span.GetAttribute("rpc.method");
            var id = span.GetAttribute("rpc.orleans.source_id");

            //rpc.system: orleans
            //rpc.service: Sample.IPingGrain
            //rpc.method: Ping

            //rpc.orleans.target_id: ping/abc
            //rpc.orleans.source_id: sys.client/hosted-127.0.0.1:11111@27774382
            //status: Ok
            if (id != "")
            {
                description =
                    new SpanDescription(model,span.ServiceName, service, method, ComponentKind.Actor, CallKind.Sync,
                        componentStack: "Microsoft Orleans");
                return true;
            }

            description =
                new SpanDescription(model,span.ServiceName, service + "Client", method, ComponentKind.Service, CallKind.Sync,
                    componentStack: "Microsoft Orleans");
            return true;
        }

        return false;
    }
}