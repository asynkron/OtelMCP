using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

public interface IExtractor
{
    bool Extract(TraceLensModel model, Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth);
}