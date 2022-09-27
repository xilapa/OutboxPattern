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
            catch (Exception e)
            {
                retry = true;
                retries++;
                logger.LogError("{CurrentTime}: Internal dictionary is too big! Cant add more messages. Current try: {Retries}", DateTime.UtcNow, retries);

                if (retries > maxRetries) throw new ConcurrentDicitionaryException("Concurrent dictionary failed. See inner exception for details", e);

                await Task.Delay(100);
            }
        } while (retry);
    }
}

public class ConcurrentDicitionaryException : Exception
{
    public ConcurrentDicitionaryException() : base()
    {
    }

    public ConcurrentDicitionaryException(string? message) : base(message)
    {
    }

    public ConcurrentDicitionaryException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}