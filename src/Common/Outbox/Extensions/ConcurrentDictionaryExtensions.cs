using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Common.Outbox.Extensions;

public static class ConcurrentDictionaryExtensions
{
    public static async ValueTask AddWithRetries<K,V>(this ConcurrentDictionary<K,V> dict, K key, V value, ILogger logger, int maxRetries = 5)
    where K : notnull
    {
        var retry = false;
        var retries = 0;
        do
        {
            try
            {
                dict.TryAdd(key, value);
            }
            catch (OverflowException)
            {
                retry = true;
                retries++;
                logger.LogError("Internal dictionary is too big! Cant add more messages. Current try {Retries}", retries);

                if (retries > maxRetries) throw;

                await Task.Delay(100);
            }
        } while (retry);
    }
}