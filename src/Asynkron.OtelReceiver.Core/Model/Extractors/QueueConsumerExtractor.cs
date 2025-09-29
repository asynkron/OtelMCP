using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class QueueConsumerExtractor : IExtractor
{
    private bool GetKafkaClient(TraceLensModel model,Span span, out SpanDescription? description)
    {
        /*
Attr messaging.kafka.message.offset   
Attr messaging.client_id   consumer-frauddetectionservice-1
Attr thread.name   main
Attr messaging.destination.name   orders
Attr messaging.operation   process
Attr messaging.system   kafka
Attr messaging.kafka.consumer.group   frauddetectionservice
         */
    
        var consumerGroup = span.GetAttribute("messaging.kafka.consumer.group");
        if (consumerGroup == "")
        {
            description = null;
            return false;
        }
        
        var peerService = span.GetAttribute("span.kind");
        
        // foreach (var a in span.Attributes)
        // {
        //     Console.WriteLine("Attr " + a.Key + "   " + a.Value);
        // }

        // if (span.Parent != null)
        // {
        //     foreach (var a in span.Parent.Attributes)
        //     {
        //         Console.WriteLine("Attr " + a.Key + "   " + a.Value);
        //     }
        // }
        var serviceName = span.ServiceName;
        description = new SpanDescription(model,serviceName, consumerGroup, span.OperationName, ComponentKind.QueueConsumer, CallKind.Async,
            componentStack: "Kafka");
        return true;
    }
    
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        depth = 0;
        description = null;

        if (GetKafkaClient(model,span, out description!)) return true;

        var messagingSystem = span.GetAttribute("messaging.system");

        if (messagingSystem == "") return false;
        
        var peerService = span.GetAttribute("span.kind");
        
        if (peerService != "consumer") return false;
        
        var destination = span.GetAttribute("messaging.destination");
        if (destination == "") destination = span.GetAttribute("messaging.destination.name");
        
        if (destination == "") return false;
        description =
            new SpanDescription(model,span.ServiceName, destination, "", ComponentKind.QueueConsumer, CallKind.Async,
                componentStack: messagingSystem);
        return true;
    }
}