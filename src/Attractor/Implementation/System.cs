using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Attractor.Implementation;

public static class System
{
    public static ISystem Create(IQueue queue, IScheduler scheduler, ILogger logger)
    {
        return new SystemImpl();
    }

    private sealed class SystemImpl : ISystem
    {
        private readonly CommandQueue<ICommand> commands = new();
        private readonly LinkedList<ProcessBuilder> builders = new();
        private readonly Dictionary<IAddress, Process> processes = new();

        private readonly IQueue queue;
        private readonly IScheduler scheduler;
        private readonly ILogger logger;

        private CancellationTokenSource cancellation;
        private Task processingTask;
        private Task stoppingTask;

        public void Register(IAddressPolicy policy, Action<IActorBuilder> configuration = null)
        {
            ArgumentNullException.ThrowIfNull(policy, nameof(policy));

            var builder = new ActorBuilder();

            configuration?.Invoke(builder);

            commands.Schedule(Command.Create(() =>
            {
                builders.AddFirst(new ProcessBuilder(policy, builder, this));
            }));
        }

        private Task<Process> GetOrCreateProcessAsync(IAddress address, IHandle handle)
        {
            var completion = new TaskCompletionSource<Process>();

            commands.Schedule(Command.Create(() =>
            {
                if (cancellation.IsCancellationRequested)
                {
                    completion.SetCanceled(cancellation.Token);
                }
                else if (processes.TryGetValue(address, out var result))
                {
                    completion.SetResult(result);
                }
                else
                {
                    commands.Schedule(CreateTryBuildProcessCommand(completion, builders.First, address));
                }
            }));

            return completion.Task;
        }

        private ICommand CreateTryBuildProcessCommand(TaskCompletionSource<Process> completion, LinkedListNode<ProcessBuilder> node, IAddress address)
        {
            return Command.Create(() =>
            {
                try
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        completion.SetCanceled(cancellation.Token);
                    }
                    else if (processes.TryGetValue(address, out var result))
                    {
                        completion.SetResult(result);
                    }
                    else if (node == null)
                    {
                        completion.SetException(new InvalidOperationException());
                    }
                    else if (node.Value.IsMatch(address))
                    {
                        var builder = node.Value;
                        var awaiter = scheduler.TryAcquireAsync(address, cancellation.Token).GetAwaiter();

                        awaiter.OnCompleted(() =>
                        {
                            try
                            {
                                var result = awaiter.GetResult();

                                if (result.Success)
                                {
                                    commands.Schedule(CreateBuildProcessCommand(completion, builder, address, result.Result));
                                }
                                else
                                {
                                    completion.SetException(new InvalidOperationException());
                                }
                            }
                            catch (Exception ex)
                            {
                                completion.SetException(ex);
                            }
                        });
                    }
                    else
                    {
                        commands.Schedule(CreateTryBuildProcessCommand(completion, node.Next, address));
                    }
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });
        }

        private ICommand CreateBuildProcessCommand(TaskCompletionSource<Process> completion, ProcessBuilder builder, IAddress address, IHandle handle)
        {
            return Command.Create(() =>
            {
                if (cancellation.IsCancellationRequested)
                {
                    completion.SetCanceled(cancellation.Token);
                }
                else if (processes.TryGetValue(address, out var result))
                {
                    completion.SetResult(result);
                }
                else
                {
                    var process = builder.Build(address, handle);

                    processes.Add(address, process);
                    completion.SetResult(process);
                }
            });
        }

        private sealed class ProcessBuilder(IAddressPolicy addressPolicy, ActorBuilder actorBuilder, SystemImpl actorSystem)
        {
            public bool IsMatch(IAddress address)
            {
                return addressPolicy.IsMatch(address);
            }

            public Process Build(IAddress address, IHandle handle)
            {
                var actor = actorBuilder.Build();
                var disposing = Disposable.CreateAsync(async () =>
                {
                    await handle.DisposeAsync();

                    var completion = new TaskCompletionSource();

                    actorSystem.commands.Schedule(Command.Create(() =>
                    {
                        actorSystem.processes.Remove(address);
                        completion.SetResult();
                    }));

                    await completion.Task;
                });

                return new Process(actor, disposing, actorSystem.cancellation.Token);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource();

            commands.Schedule(Command.Create(() =>
            {
                if (cancellation.IsCancellationRequested)
                {
                    completion.SetCanceled(cancellation.Token);
                }
                else if (processingTask == null)
                {
                    cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    processingTask = Task.Run(ExecuteAsync);

                    completion.SetResult();
                }
            }));

            return completion.Task;
        }

        private async Task ExecuteAsync()
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {

                }
                catch (Exception e)
                {

                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource();

            commands.Schedule(Command.Create(() =>
            {
                cancellation.Cancel();
                completion.SetResult();
            }));


            if (processingTask == null)
            {
                return;
            }

            cancellation.Cancel();

            await processingTask;

            foreach (var pair in processes)
            {
                await pair.Value.Value.DisposeAsync();
            }

            processes.Clear();
        }

        private async Task RunStoppingAsync()
        {

        }
    }
}