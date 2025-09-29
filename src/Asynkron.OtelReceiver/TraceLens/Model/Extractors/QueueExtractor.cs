namespace TraceLens.Model.Extractors;

internal class QueueExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, out SpanDescription description, out int depth)
    {
        depth = 0;
        description = null!;
        if (GetAzureStorageQueue(model,span, out description!)) return true;
        
        //don´t treat kafka consumers as producers
        var consumerGroup = span.GetAttribute("messaging.kafka.consumer.group");
        if (consumerGroup != "")
        {
            return false;
        }

        var messagingSystem = span.GetAttribute("messaging.system");
        if (messagingSystem == "") return false;
        var destination = span.GetAttribute("messaging.destination");
        
        if (destination == "") 
            destination = span.GetAttribute("messaging.destination.name");
        
        if (destination == "") return false;
        
        // foreach (var a in span.Attributes)
        // {
        //     Console.WriteLine("Attr " + a.Key + "   " + a.Value);
        // }
        // Console.WriteLine("----");
        // if (span.Parent != null)
        // {
        //     foreach (var a in span.Parent.Attributes)
        //     {
        //         Console.WriteLine("Attr " + a.Key + "   " + a.Value);
        //     }
        // }

        description = new SpanDescription(model,messagingSystem, destination, "", ComponentKind.Queue, CallKind.Async,
            componentStack: messagingSystem);
        return true;
    }

    
    
    private bool GetAzureStorageQueue(TraceLensModel model,Span entry, out SpanDescription? fullName)
    {
        fullName = null;
        var url = entry.GetAttribute("http.url");
        if (!url.Contains(".queue.core.windows.net")) return false;

        var uri = new Uri(url);
        var path = uri.Segments[1].TrimEnd('/');
        fullName = new SpanDescription(model,"Azure", path, "", ComponentKind.Queue, CallKind.Async,
            componentStack: "Azure Storage Queue");
        return true;
    }
}