using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class ReceivingTimeoutHandler : BaseHandlerDecorator
    {
        private readonly TimeSpan timeout;

        private Timer timer;
        private long lastCallTime;

        public ReceivingTimeoutHandler(TimeSpan timeout)
        {
            this.timeout = timeout;
        }

        public override async ValueTask OnStartAsync(IContext context)
        {
            var selfRef = context.Get<IRef>();

            Interlocked.Exchange(ref lastCallTime, DateTime.Now.Ticks);

            timer = new Timer(self =>
            {
                var now = DateTime.Now;
                var callTime = Interlocked.Read(ref lastCallTime);

                if (callTime == TimeSpan.Zero.Ticks)
                {
                    timer.Change(timeout, Timeout.InfiniteTimeSpan);
                }
                else if (now.Ticks - callTime >= timeout.Ticks)
                {
                    ((IRef)self).Cancel();
                }
                else
                {
                    timer.Change(TimeSpan.FromTicks(callTime + timeout.Ticks - now.Ticks), Timeout.InfiniteTimeSpan);
                }
            }, selfRef, timeout, Timeout.InfiniteTimeSpan);

            selfRef.GetToken().Register(timer.Dispose);

            await base.OnStartAsync(context);
        }

        public override async ValueTask OnReceiveAsync(IContext context)
        {
            Interlocked.Exchange(ref lastCallTime, TimeSpan.Zero.Ticks);
            
            try
            {
                await base.OnReceiveAsync(context);
            }
            finally
            {
                Interlocked.Exchange(ref lastCallTime, DateTime.Now.Ticks);
            }
        }
    }
}
