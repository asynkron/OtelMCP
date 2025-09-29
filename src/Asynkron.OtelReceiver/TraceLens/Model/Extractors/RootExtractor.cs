using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class RootExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        description = null;
        depth = 0;
        if (span.TraceId == "root")
        {
            description = new SpanDescription(model,"", "Start", "", ComponentKind.Start, CallKind.Sync);
            return true;
        }

        return false;
    }
}