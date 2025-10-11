using System;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal static class Command
{
    public static ICommand From(Action strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        return new StrategyInstance(strategy);
    }

    public static Task ScheduleAsync(this ICommandQueue queue, ICommand command)
    {
        return queue.ScheduleAsync(command.Execute);
    }

    public static Task ScheduleAsync(this ICommandQueue queue, Action action)
    {
        var completion = new TaskCompletionSource();

        queue.Schedule(From(() =>
        {
            try
            {
                action.Invoke();
                completion.SetResult();
            }
            catch (OperationCanceledException e)
            {
                completion.SetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        }));

        return completion.Task;
    }

    public static Task<T> ScheduleAsync<T>(this ICommandQueue queue, Func<T> func)
    {
        var completion = new TaskCompletionSource<T>();

        queue.Schedule(From(() =>
        {
            try
            {
                completion.SetResult(func.Invoke());
            }
            catch (OperationCanceledException e)
            {
                completion.SetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        }));

        return completion.Task;
    }

    private class StrategyInstance(Action strategy) : ICommand
    {
        void ICommand.Execute() => strategy();
    }
}