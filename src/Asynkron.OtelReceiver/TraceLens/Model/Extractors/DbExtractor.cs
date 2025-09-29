using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TraceLens.Model.Extractors;

internal class DbExtractor : IExtractor
{
    public bool Extract(TraceLensModel model,Span span, [NotNullWhen(true)] out SpanDescription? description, out int depth)
    {
        depth = 0;
        description = null;
        if (GetCosmosDb(model, span, out description)) return true;

        var dbModel = span.GetAttribute("db.system");
        if (dbModel != "")
        {
            var dbName = span.GetAttribute("db.name");
            var dbStatement = span.GetAttribute("db.statement");

            var dbName2 = ExtractDatabaseName(dbStatement);
            if (dbName2 != "")
            {
                dbName = dbName2;
            }

            var dbStatementParts = dbStatement.Split([' ', '\n']);
            var dbStatementShort = dbStatementParts[0];
            try
            {
                if (dbStatementShort.Length > 10)
                {
                    dbStatementShort = dbStatementShort.Substring(0, 10);
                }

                if (dbStatementShort.ToLowerInvariant() == "create")
                {
                    dbStatementShort += " " + dbStatementParts[1]; // + " " + dbStatementParts[2];
                }
            }
            catch
            {
                //pass
            }

            if (dbName != "")
            {
                description = new SpanDescription(model, dbModel, dbName, dbStatementShort, ComponentKind.Database,
                    CallKind.Sync,
                    componentStack: dbModel);
                return true;
            }

            description = new SpanDescription(model, dbModel, dbModel, dbStatementShort, ComponentKind.Database, CallKind.Sync,
                componentStack: dbModel);
            return true;
        }

        return false;
    }

    static string ExtractDatabaseName(string sql)
    {
        // Regex pattern to find 'CREATE DATABASE' followed by any amount of whitespace,
        // and then capture the following sequence of word characters as the database name.
        var pattern = @"CREATE DATABASE\s+(\w+);";

        // Use Regex.Match to find a match in the provided SQL statement.
        var match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            // If a match is found, the database name is in the first capturing group.
            return match.Groups[1].Value;
        }

        // Return null if no match is found.
        return "";
    }

    private bool GetCosmosDb(TraceLensModel model,Span entry, [NotNullWhen(true)] out SpanDescription? fullName)
    {
        fullName = null;
        var url = entry.GetAttribute("http.url");
        if (url.Contains("documents.azure.com"))
        {
            fullName = fullName = new SpanDescription(model,"Azure", "CosmosDB", "", ComponentKind.Database, CallKind.Sync,
                componentStack: "Azure CosmosDB");
            return true;
        }

        return false;
    }
}