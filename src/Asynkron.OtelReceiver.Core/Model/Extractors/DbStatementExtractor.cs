using System.Diagnostics.CodeAnalysis;

namespace TraceLens.Model.Extractors;

internal class DbStatementExtractor : IExtractor
{
    public bool Extract(TraceLensModel model, Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        description = null;
        depth = 0;
        var kind = span.GetAttribute("tracelens.kind");
        if (kind == "") return false;
        var statement = span.GetAttribute("db.statement");

        description = new SpanDescription(model, "mssql", statement, "", ComponentKind.DatabaseStatement, CallKind.Sync);

        return true;
    }
}