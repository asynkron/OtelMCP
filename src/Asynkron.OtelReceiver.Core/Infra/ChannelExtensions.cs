using System.Threading.Channels;

namespace TraceLens.Infra;

public static class ChannelExtensions
{
    public static async ValueTask<List<T>> ReadBatchAsync<T>(this ChannelReader<T> reader, int max)
    {
        await reader.WaitToReadAsync();
        var results = new List<T>(max);
        var i = 3;
        while (true)
        {
            while (
                results.Count < max
                && reader.TryRead(out var item))
                results.Add(item);

            if (results.Count == max)
                return results;

            if (i > 0)
            {
                await Task.Delay(100);
                i--;
            }
            else
            {
                return results;
            }
        }
    }
}