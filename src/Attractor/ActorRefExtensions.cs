using System;
using System.Threading;
using Attractor.Implementation;

namespace Attractor
{
    public static class ActorRefExtensions
    {
        public static void Stop(this IActorRef actorRef)
        {
            ArgumentNullException.ThrowIfNull(actorRef, nameof(actorRef));
            
            actorRef.Send(Payload.FromType(StoppingMessage.Instance));
        }

        public static IActorRef WithSendingTimeout(this IActorRef actorRef, TimeSpan timeout, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(actorRef, nameof(actorRef));
            
            return SendingTimeoutDecorator.Start(timeout, actorRef, token);
        }

        private class SendingTimeoutDecorator : IActorRef
        {
            private readonly ReaderWriterLockSlim sync = new();

            private readonly IActorRef actorRef;
            private readonly TimeSpan timeout;
            private readonly CancellationToken cancellationToken;

            private CancellationTokenRegistration cancellationRegistration;
            private long lastProcessedTime;
            private volatile bool isStopped;
            private volatile Timer timeoutTimer;

            private SendingTimeoutDecorator(TimeSpan timeout, IActorRef actorRef, CancellationToken cancellationToken)
            {
                this.timeout = timeout;
                this.actorRef = actorRef;
                this.cancellationToken = cancellationToken;
            }

            void IActorRef.Send(IPayload payload, Action<IContext> configuration)
            {
                sync.EnterReadLock();

                try
                {
                    if (isStopped)
                    {
                        throw new TimeoutException();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    
                    actorRef.Send(payload, configuration);
                }
                finally
                {
                    Interlocked.Exchange(ref lastProcessedTime, DateTime.Now.Ticks);
                    
                    sync.ExitReadLock();
                }
            }

            public static SendingTimeoutDecorator Start(TimeSpan timeout, IActorRef actorRef, CancellationToken token)
            {
                var result = new SendingTimeoutDecorator(timeout, actorRef, token)
                {
                    lastProcessedTime = DateTime.Now.Ticks
                };

                result.timeoutTimer = new Timer(result.OnTimeout, null, timeout, Timeout.InfiniteTimeSpan);
                result.cancellationRegistration = token.Register(result.OnCancel);

                return result;
            }

            private void OnCancel()
            {
                sync.EnterWriteLock();

                try
                {
                    timeoutTimer.Dispose();
                    cancellationRegistration.Dispose();
                }
                finally
                {
                    sync.ExitWriteLock();
                }
            }

            private void OnTimeout(object state)
            {
                sync.EnterWriteLock();

                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    timeoutTimer.Dispose();
                    
                    var currentTime = TimeSpan.FromTicks(DateTime.Now.Ticks);
                    var currentLastProcessedTime = TimeSpan.FromTicks(Interlocked.Read(ref lastProcessedTime));
                    var diff = currentTime - currentLastProcessedTime;

                    if (diff >= timeout)
                    {
                        isStopped = true;

                        cancellationRegistration.Dispose();
                        actorRef.Stop();
                    }
                    else
                    {
                        timeoutTimer = new Timer(OnTimeout, null, timeout - diff, Timeout.InfiniteTimeSpan);
                    }
                }
                finally
                {
                    sync.ExitWriteLock();
                }
            }
        }

        public static IActorRef WithSendingLimit(this IActorRef actorRef, int limit)
        {
            ArgumentNullException.ThrowIfNull(actorRef, nameof(actorRef));

            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }
            
            return new SendingLimitDecorator(actorRef, limit);
        }

        private class SendingLimitDecorator : IActorRef
        {
            private readonly IActorRef actorRef;
            private readonly int limit;

            private int count;

            public SendingLimitDecorator(IActorRef actorRef, int limit)
            {
                this.actorRef = actorRef;
                this.limit = limit;
            }

            void IActorRef.Send(IPayload payload, Action<IContext> configuration)
            {
                var current = Interlocked.Increment(ref count);
                
                if (current > limit)
                {
                    throw new ArgumentOutOfRangeException(nameof(payload));
                }
                else
                {
                    actorRef.Send(payload, configuration);
                }

                if (current == limit)
                {
                    actorRef.Stop();
                }
            }
        }

        public static IActorRef WithTtl(this IActorRef actorRef, TimeSpan ttl, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(actorRef, nameof(actorRef));

            return TtlDecorator.Start(actorRef, ttl, token);
        }

        private class TtlDecorator : IActorRef
        {
            private readonly ReaderWriterLockSlim sync = new();
            
            private readonly IActorRef actorRef;
            private readonly CancellationToken cancellationToken;
            private readonly CancellationTokenSource timeoutSource;

            private CancellationTokenRegistration cancellationRegistration;
            private volatile bool isStopped;

            private TtlDecorator(IActorRef actorRef, CancellationTokenSource timeoutSource, CancellationToken cancellationToken)
            {
                this.actorRef = actorRef;
                this.cancellationToken = cancellationToken;
                this.timeoutSource = timeoutSource;
            }

            void IActorRef.Send(IPayload payload, Action<IContext> configuration)
            {
                sync.EnterReadLock();

                try
                {
                    if (isStopped)
                    {
                        throw new TimeoutException();
                    }
                    
                    cancellationToken.ThrowIfCancellationRequested();

                    actorRef.Send(payload, configuration);
                }
                finally
                {
                    sync.ExitReadLock();
                }
            }

            private void OnCancel()
            {
                sync.EnterWriteLock();

                try
                {
                    cancellationRegistration.Dispose();
                    timeoutSource.Dispose();
                }
                finally
                {
                    sync.ExitWriteLock();
                }
            }

            private void OnTimeout()
            {
                sync.EnterWriteLock();

                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    isStopped = true;

                    cancellationRegistration.Dispose();
                    timeoutSource.Dispose();
                    actorRef.Stop();
                }
                finally
                {
                    sync.ExitWriteLock();
                }
            }

            public static TtlDecorator Start(IActorRef actorRef, TimeSpan ttl, CancellationToken token)
            {
                var source = new CancellationTokenSource(ttl);
                var result = new TtlDecorator(actorRef, source, token);

                result.cancellationRegistration = token.Register(result.OnCancel);

                source.Token.Register(result.OnTimeout);

                return result;
            }
        }
    }
}