namespace CIS.IntegrationTests.Infrastructure;

internal static class ConcurrentRequests
{
    public static async Task<T[]> RunAsync<T>(int count, Func<int, Task<T>> operation)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(async index =>
        {
            await start.Task;
            return await operation(index);
        }).ToArray();

        // All operations await the gate before any is released.
        start.SetResult();
        return await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(60));
    }
}
