using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation;

namespace Attractor
{
    public static class BuilderExtensions
    {
        public static void MessageProcessingTimeout(this IActorBuilder builder, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            builder.DecorateActor(() =>
            {
                return Actor.FromStrategy(async (next, context, token) =>
                {
                    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);

                    cancellation.CancelAfter(timeout);

                    await next(context, cancellation.Token);
                });
            });
        }

        public static void ProcessReceivingTimeout(this IActorBuilder builder, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            builder.DecorateActor(() =>
            {
                var command = new MessageReceivingTimeoutCommand
                {
                    Timer = new Stopwatch(),
                    Timeout = timeout,
                    Cancellation = new CancellationTokenSource(),
                    Commands = new CommandQueue<MessageReceivingTimeoutCommand>()
                };

                return Actor.FromStrategy(
                    onReceive: async (next, context, token) =>
                    {
                        try
                        {
                            await next(context, token);
                        }
                        finally
                        {
                            command.Process = context.Get<IActorProcess>();

                            command.Commands.Schedule(command);
                        }
                    },
                    onDispose: async (next) =>
                    {
                        try
                        {
                            await next();
                        }
                        finally
                        {
                            command.Cancellation.Cancel();
                        }
                    });
            });
        }

        private struct MessageReceivingTimeoutCommand : ICommand
        {
            public bool IsCallback { get; set; }

            public IActorProcess Process { get; set; }

            public Stopwatch Timer { get; init; }

            public TimeSpan Timeout { get; init; }

            public CancellationTokenSource Cancellation { get; init; }

            public CommandQueue<MessageReceivingTimeoutCommand> Commands { get; init; }
            
            readonly ValueTask ICommand.ExecuteAsync()
            {
                if (IsCallback)
                {
                    Timer.Stop();

                    if (Timer.Elapsed >= Timeout)
                    {
                        Process.Stop();
                    }
                    else
                    {
                        Start(Timeout - Timer.Elapsed, this);
                        Timer.Start();
                    }
                }
                else
                {
                    if (!Timer.IsRunning)
                    {
                        Start(Timeout, this);
                    }

                    Timer.Restart();
                }

                return default;
            }

            private static void Start(TimeSpan delay, MessageReceivingTimeoutCommand command)
            {
                Task.Delay(delay, command.Cancellation.Token).ContinueWith(_ => 
                {
                    command.IsCallback = true;
                    command.Commands.Schedule(command);
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }

        public static void Chain<T>(this IActorBuilder builder, Func<T> factory) where T : class, IActor
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(factory, nameof(factory));

            builder.DecorateActor(() => ChainActor(factory()));
        }

        public static void Chain<T>(this IServicesActorBuilder builder, Func<IServiceProvider, T> factory) where T : class, IActor
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(factory, nameof(factory));

            builder.DecorateActor(provider => ChainActor(factory(provider)));
        }

        private static IActorDecorator ChainActor(IActor actor)
        {
            return Actor.FromStrategy(
                async (next, context, token) => 
                {
                    await actor.OnReceiveAsync(context, token);
                    await next(context, token);
                },
                async (next) =>
                {
                    await actor.DisposeAsync();
                    await next();
                });
        }
    }
}