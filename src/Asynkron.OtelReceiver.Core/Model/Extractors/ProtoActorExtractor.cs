namespace TraceLens.Model.Extractors;

internal class ProtoActorExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, out SpanDescription description, out int depth)
    {
        depth = 0;
        description = null!;

        var action = span.GetAttribute("proto.action");
        var actor = span.GetAttribute("proto.actortype");
        if (actor is "") return false;
        
        if (action == "SpawnNamed")
        {
            description = new SpanDescription(model,span.ServiceName, actor, "SpawnNamed", ComponentKind.Actor, CallKind.Sync, "",
                "Proto.Actor");
            return true;
        }
        
        if (action == "Spawn")
        {
            description = new SpanDescription(model,span.ServiceName, actor, "Spawn", ComponentKind.Actor, CallKind.Sync, "",
                "Proto.Actor",false);
            return true;
        }
        
        var requestType = span.GetAttribute("proto.messagetype");

        var componentKind = ComponentKind.Actor;
        var isClient = false;
        if (actor == "<None>")
        {
            componentKind = ComponentKind.Service;
            actor = "RootContext";
            isClient = true;
        }
        var callKind = CallKind.Sync;
        
        var responseType = span.GetAttribute("proto.responsemessagetype");

        if (span.OperationName.Contains(".Request "))
            callKind = CallKind.Sync; //TODO: fix, how to connect request to response with span?
        if (span.OperationName.Contains(".RequestAsync ")) callKind = CallKind.Sync;
        if (span.OperationName.Contains(".Send ")) callKind = CallKind.Async;


        description = new SpanDescription(model,span.ServiceName, actor, requestType, componentKind, callKind, responseType,
            "Proto.Actor", isClient);
        return true;
    }
}

internal class ProtoActorChildExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, out SpanDescription description, out int depth)
    {
        description = null!;
        (var actor, depth) = span.GetParentTag("proto.actortype");
        if (actor is "") return false;

        if (actor == "<None>") actor = "RootContext";

        description = new SpanDescription(model,span.ServiceName, actor, span.OperationName, ComponentKind.Actor,
            CallKind.Sync);
        return true;
    }
}

internal class ProtoActorEventExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, out SpanDescription description, out int depth)
    {
        depth = 0;
        description = null!;
        var requestType = span.GetAttribute("proto.messagetype");
        
        
        if (span.OperationName.Contains("EventStream"))
        {
            description = new SpanDescription(model,span.ServiceName, "EventStream", requestType, ComponentKind.Service, CallKind.Sync, "",
                "Proto.Actor", false);
            return true;
        }
        
        if (span.OperationName.Contains("Subscriber"))
        {
            var eventSubscriber = span.GetAttribute("proto.eventsubscriber");
            description = new SpanDescription(model,span.ServiceName, "Subscriber:" + eventSubscriber, requestType, ComponentKind.Service, CallKind.Sync, "",
                "Proto.Actor", false);
            return true;
        }

        return false;
    }
}