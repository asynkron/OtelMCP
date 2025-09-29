using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class TemporalExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        depth = 0;
        description = null!;
        var temporalWorkflowId = span.GetAttribute("temporalWorkflowID");
        var temporalActivityId = span.GetAttribute("temporalActivityID");
        if (temporalWorkflowId == "") return false;


        if (span.OperationName.StartsWith("RunWorkflow"))
        {
            var parts = span.OperationName.Split(":");
            var workflowName = parts[1];
            description = new SpanDescription(model,"Temporal", workflowName, "Run Workflow", ComponentKind.Workflow,
                CallKind.Sync, componentStack: "Temporal.IO");
            return true;
        }

        if (span.OperationName.StartsWith("StartActivity"))
        {
            var parts = span.OperationName.Split(":");
            var activityName = parts[1];
            description = new SpanDescription(model,"Temporal", activityName, "Start Activity", ComponentKind.Activity,
                CallKind.Sync, componentStack: "Temporal.IO");
            return true;
        }

        if (span.OperationName.StartsWith("RunActivity"))
        {
            var parts = span.OperationName.Split(":");
            var activityName = parts[1];
            description = new SpanDescription(model,"Temporal", activityName, "Run Activity", ComponentKind.Activity,
                CallKind.Sync, componentStack: "Temporal.IO");
            return true;
        }


        return false;
    }
}