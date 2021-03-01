using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Extensions
{
    public static class TaskExtensions
    {
        public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
        {
            if (task == null)
                throw new Exception("task is null");

            using var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token))
                .ConfigureAwait(false);
            if (completedTask != task)
                throw new TimeoutException("The operation has timed out.");
            timeoutCancellationTokenSource.Cancel();
            return await task.ConfigureAwait(false); // Very important in order to propagate exceptions                
        }
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            if (task == null)
                throw new Exception("task is null");

            using var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token))
                .ConfigureAwait(false);
            if (completedTask != task)
                throw new TimeoutException("The operation has timed out.");
            timeoutCancellationTokenSource.Cancel();
            await task.ConfigureAwait(false); // Very important in order to propagate exceptions                
        }
    }
}