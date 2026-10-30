namespace MagDeer.Core;

public static class ShutdownDrain
{
    /// <summary>Observe failures, but never wait indefinitely for a canceled network/file task.</summary>
    public static async Task<bool> WaitAsync(IEnumerable<Task> tasks, TimeSpan limit)
    {
        var all = Task.WhenAll(tasks);
        try { await all.WaitAsync(limit).ConfigureAwait(false); return true; }
        catch (TimeoutException)
        {
            _ = all.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return false;
        }
        catch { return true; } // All tasks have ended; their failure is observed.
    }
}
