using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace TraceLens.Model;

public static class StringKey
{
    public static string Get(string x,TraceLensModel model)
    {
        return model.Keys.GetOrAdd(x, _ => $"ID{model.Keys.Count}");
    }
}