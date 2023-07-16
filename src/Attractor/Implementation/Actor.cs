using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Actor
    {
        private static readonly IActor empty = new Instance(null, null);
        
        public static IActorDecorator FromStrategy(OnReceiveDecorator onReceive = null, OnDisposeDecorator onDispose = null)
        {
            return new Decorator(onReceive, onDispose);
        }

        public static IActor FromString(
            Func<string, CancellationToken, ValueTask> onReceive = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance<string>(onReceive, onDispose);
        }

        public static IActor FromBytes(
            Func<byte[], CancellationToken, ValueTask> onReceive = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance<byte[]>(onReceive, onDispose);
        }

        public static IActor FromStrategy(OnReceive onReceive = null, OnDispose onDispose = null)
        {
            return new Instance(onReceive, onDispose);
        }

        public static IActorDecorator Chain(IActor actor)
        {
            ArgumentNullException.ThrowIfNull(actor, nameof(actor));

            return FromStrategy(
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

        public static IActorDecorator ProcessingTimeout(TimeSpan timeout)
        {
            return FromStrategy(async (next, context, token) =>
            {
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);

                cancellation.CancelAfter(timeout);

                await next(context, cancellation.Token);
            });
        }

        public static IActorDecorator ReceivingTimeout(TimeSpan timeout, Predicate<IContext> match = null)
        {
            var command = new MessageReceivingTimeoutCommand
            {
                Timer = new Stopwatch(),
                Timeout = timeout,
                Cancellation = new CancellationTokenSource(),
                Commands = new CommandQueue<MessageReceivingTimeoutCommand>(),
                IsMatch = true
            };

            return FromStrategy(
                onReceive: async (next, context, token) =>
                {
                    try
                    {
                        await next(context, token);
                    }
                    finally
                    {
                        command.Process = context.Get<IActorProcess>();
                        
                        if (match != null)
                        {
                            command.IsMatch = match(context);
                        }

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
        }

        public static IActor Empty()
        {
            return empty;
        }

        private record Decorator(OnReceiveDecorator OnReceive, OnDisposeDecorator OnDispose) : IActorDecorator
        {
            private OnReceive onReceive;
            private OnDispose onDispose;

            void IDecorator<IActor>.Decorate(IActor value)
            {
                onReceive = value.OnReceiveAsync;
                onDispose = value.DisposeAsync;
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose(onDispose) : onDispose();
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return OnReceive != null ? OnReceive(onReceive, context, token) : onReceive(context, token);
            }
        }

        private record Instance(OnReceive OnReceive, OnDispose OnDispose) : IActor
        {
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose() : default;
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return OnReceive != null ? OnReceive(context, token) : default;
            }
        }

        private record Instance<TType>(
            Func<TType, CancellationToken, ValueTask> OnReceive,
            Func<ValueTask> OnDispose) : IActor, IVisitor
        {
            private TType acceptedValue;
            private bool isAccepted;
            
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose() : default;
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                if (!TryAccept(context))
                {
                    return default;
                }

                return OnReceive != null ? OnReceive(acceptedValue, token) : default;
            }

            private bool TryAccept(IContext context)
            {
                isAccepted = false;
                
                var payload = context.Get<IPayload>();

                if (payload == null)
                {
                    return false;
                }

                payload.Accept(this);

                return isAccepted;
            }

            void IVisitor.Visit<TValue>(TValue value)
            {
                if (value is TType result)
                {
                    acceptedValue = result;
                    isAccepted = true;
                }
            }
        }

        private struct MessageReceivingTimeoutCommand : ICommand
        {
            public bool IsCallback { get; set; }

            public bool IsMatch { get; set; }

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
                        Timer.Start();
                    }
                    else if (IsMatch)
                    {
                        Timer.Restart();
                    }
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
    }
}