// namespace TraceLens.Model.Extractors;
//
// internal class AkkaExtractor : IExtractor
// {
//     public bool Extract(Span span, out SpanDescription description, out int depth)
//     {
//         depth = 0;
//         description = null!;
//         var actor = span.GetTag("akka.actor-type");
//         if (actor is "") return false;
//         var componentKind = ComponentKind.Actor;
//         if (actor == "<None>")
//         {
//             componentKind = ComponentKind.Service;
//             actor = "???";
//         }
//
//         var callKind = CallKind.Async;
//         var messageType = span.GetTag("akka.message-type");
//         var verb = span.OperationName;
//         var groupName = span.ServiceName;
//         if (span.OperationName.Contains(".Tell "))
//         {
//             callKind = CallKind.Async;
//             verb = "";
//         }
//
//         if (span.OperationName.Contains(".Ask "))
//         {
//             callKind = CallKind.Sync;
//             verb = "";
//         }
//
//         if (span.OperationName.Contains(".ReceiveMessage "))
//         {
//             callKind = span.Parent.Description.CallKind;
//             verb = "";
//         }
//
//         var name = verb + messageType;
//
//         description = new SpanDescription(groupName, actor, name, componentKind, callKind, componentStack: "Akka.NET");
//         return true;
//     }
// }

