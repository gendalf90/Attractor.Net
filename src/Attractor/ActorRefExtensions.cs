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

        private class SendingTimeoutDecorator : IActorRef
        {
            private readonly CommandQueue commands = new();
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

            async ValueTask IActorRef.PostAsync(IPayload payload, Action<IContext> configuration, CancellationToken token)
            {
                try
                {
                    await actorRef.PostAsync(payload, configuration, token);
                }
                finally
                {
                    commands.Schedule(new StrategyCommand(() =>
                    {
                        if (!timer.IsRunning)
                        {
                            Start(timeout);
                        }

                        timer.Restart();
                    }));
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
                            await actorRef.PostAsync(StoppingMessage.Instance, null, cancellation);
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

        public static async ValueTask SendAsync(this IActorRef actorRef, IPayload payload, Action<IContext> configuration = null, CancellationToken token = default)
        {
            var completion = new TaskCompletionSource();

            void supervisorConfiguration(IContext context)
            {
                configuration?.Invoke(context);

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
            }

            await actorRef.PostAsync(payload, supervisorConfiguration, token);
            
            await completion.Task.WaitAsync(token);
        }
    }
}