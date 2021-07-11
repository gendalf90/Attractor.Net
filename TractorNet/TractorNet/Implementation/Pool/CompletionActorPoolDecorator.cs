using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TractorNet.Implementation.Common;

namespace TractorNet.Implementation.Pool
{
    internal sealed class CompletionActorPoolDecorator : BaseActorPoolDecorator
    {
        private readonly object sync = new object();
        private readonly ConcurrentDictionary<int, Task> completionTasks = new ConcurrentDictionary<int, Task>();

        private bool isDisposed = false;

        public CompletionActorPoolDecorator(IActorPool pool) : base(pool)
        {
        }

        public override async ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default)
        {
            var completionSource = new TaskCompletionSource();
            var completionTask = completionSource.Task;
            var completionDisposing = new StrategyDisposable(() =>
            {
                completionSource.SetResult();
                completionTasks.TryRemove(completionTask.Id, out _);
            });

            lock (sync)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(CompletionActorPoolDecorator));
                }

                completionTasks.TryAdd(completionTask.Id, completionTask);
            }

            await using (var conditionDisposing = new ConditionDisposable(completionDisposing, true))
            {
                var result = await pool.TryUsePlaceAsync(token);

                if (result)
                {
                    conditionDisposing.Disable();
                }

                return result
                    ? new TrueResult<IAsyncDisposable>(new StrategyDisposable(async () =>
                    {
                        await result.Value.DisposeAsync();
                        await completionDisposing.DisposeAsync();
                    }))
                    : result;
            }
        }

        public override async ValueTask DisposeAsync()
        {
            lock (sync)
            {
                isDisposed = true;
            }

            await Task.WhenAll(completionTasks.Values);

            await base.DisposeAsync();
        }
    }
}
