using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation;

namespace Attractor
{
    public static class ActorRefExtensions
    {
        public static IActorRef WithSendingTimeout(this IActorRef actorRef, TimeSpan timeout, bool started = false, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(actorRef, nameof(actorRef));
            
            return new SendingTimeoutDecorator(timeout, actorRef, started, token);
        }

        private class SendingTimeoutDecorator : IActorRef, ICommand
        {
            private readonly CommandQueue<ICommand> commands = new();
            private readonly Stopwatch timer = new();

            private readonly IActorRef actorRef;
            private readonly TimeSpan timeout;
            private readonly CancellationToken cancellation;

            public SendingTimeoutDecorator(TimeSpan timeout, IActorRef actorRef, bool started, CancellationToken cancellation)
            {
                this.timeout = timeout;
                this.actorRef = actorRef;
                this.cancellation = cancellation;

                if (started)
                {
                    Start(timeout);
                    timer.Start();
                }
            }

            ValueTask ICommand.ExecuteAsync()
            {
                if (!timer.IsRunning)
                {
                    Start(timeout);
                }

                timer.Restart();

                return default;
            }

            async ValueTask IActorRef.PostAsync(IContext context, CancellationToken token)
            {
                try
                {
                    await actorRef.PostAsync(context, token);
                }
                finally
                {
                    commands.Schedule(this);
                }
            }

            private void Start(TimeSpan delay)
            {
                Task.Delay(delay, cancellation).ContinueWith((_) =>
                {
                    commands.Schedule(new StrategyCommand(async () =>
                    {
                        timer.Stop();

                        if (timer.Elapsed >= timeout)
                        {
                            var context = Context.Default();

                            context.Set<IPayload>(StoppingMessage.Instance);
                            
                            await actorRef.PostAsync(context, cancellation);
                        }
                        else
                        {
                            Start(timeout - timer.Elapsed);
                            timer.Start();
                        }
                    }));
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }

        public static async ValueTask SendAsync(this IActorRef actorRef, IContext context, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            
            var completion = new TaskCompletionSource();
            var supervisor = context.Get<ISupervisor>();

            context.Set(Supervisor.FromStrategy(
                async (context, error, token) =>
                {
                    try
                    {
                        if (supervisor != null)
                        {
                            await supervisor.OnProcessedAsync(context, error, token);
                        }
                    }
                    finally
                    {
                        if (error == null)
                        {
                            completion.SetResult();
                        }
                        else
                        {
                            completion.SetException(error);
                        }
                    }
                },
                async (context, token) =>
                {
                    try
                    {
                        if (supervisor != null)
                        {
                            await supervisor.OnStoppedAsync(context, token);
                        }
                    }
                    finally
                    {
                        completion.SetResult();
                    }
                }));

            await actorRef.PostAsync(context, token);
            await completion.Task.WaitAsync(token);
        }
    }
}