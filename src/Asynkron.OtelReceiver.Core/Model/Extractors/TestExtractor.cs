using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class TestExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        description = null;
        (var testName, depth) = span.GetParentTag("test.name");
        if (testName != "")
        {
            description =
                new SpanDescription(model,"Test", testName, span.OperationName, ComponentKind.Service, CallKind.Sync);
            return true;
        }

        return false;
    }
}