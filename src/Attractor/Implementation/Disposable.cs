using System;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal static class Disposable
{
    public static IDisposable Empty { get; } = new StrategyInstance();

    public static IAsyncDisposable EmptyAsync { get; } = new StrategyInstance();

    public static IDisposable Create(Action strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        return new StrategyInstance(sync: strategy);
    }

    public static IAsyncDisposable Create(Func<ValueTask> strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        return new StrategyInstance(async: strategy);
    }

    public static IDisposable Combine(IDisposable first, IDisposable second)
    {
        ArgumentNullException.ThrowIfNull(first, nameof(first));
        ArgumentNullException.ThrowIfNull(second, nameof(second));

        return new StrategyInstance(sync: () =>
        {
            using (second)
            {
                first.Dispose();
            }
        });
    }

    public static IAsyncDisposable Combine(IAsyncDisposable first, IAsyncDisposable second)
    {
        ArgumentNullException.ThrowIfNull(first, nameof(first));
        ArgumentNullException.ThrowIfNull(second, nameof(second));

        return new StrategyInstance(async: async () =>
        {
            await using (second)
            {
                await first.DisposeAsync();
            }
        });
    }

    public static IDisposable With(this IDisposable first, IDisposable second)
    {
        return Combine(first, second);
    }

    public static IAsyncDisposable With(this IAsyncDisposable first, IAsyncDisposable second)
    {
        return Combine(first, second);
    }

    public static IAsyncDisposable With(this IAsyncDisposable first, IDisposable second)
    {
        return Combine(first, Async(second));
    }

    public static IAsyncDisposable Async(IDisposable value)
    {
        return Create(() =>
        {
            value.Dispose();

            return ValueTask.CompletedTask;
        });
    }

    private class StrategyInstance(Action sync = null, Func<ValueTask> async = null) : IDisposable, IAsyncDisposable
    {
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return async != null ? async() : ValueTask.CompletedTask;
        }

        void IDisposable.Dispose()
        {
            sync?.Invoke();
        }
    }
}