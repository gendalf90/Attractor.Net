using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Mailbox
    {
        public static IMailboxDecorator FromStrategy(
            Func<Func<CancellationToken, ValueTask<IContext>>, CancellationToken, ValueTask<IContext>> onReceive = null,
            Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onSend = null)
        {
            return new Decorator(onReceive, onSend);
        }

        public static IMailbox FromStrategy(
            Func<CancellationToken, ValueTask<IContext>> onReceive = null,
            Func<IContext, CancellationToken, ValueTask> onSend = null)
        {
            return new Instance(onReceive, onSend);
        }
        
        public static IMailbox Default()
        {
            return new LinkedListMailbox();
        }

        private class Decorator : IMailboxDecorator
        {
            private readonly Func<Func<CancellationToken, ValueTask<IContext>>, CancellationToken, ValueTask<IContext>> onReceive;
            private readonly Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onSend;

            private IMailbox value;

            public Decorator(
                Func<Func<CancellationToken, ValueTask<IContext>>, CancellationToken, ValueTask<IContext>> onReceive,
                Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onSend)
            {
                this.onReceive = onReceive;
                this.onSend = onSend;
            }

            void IDecorator<IMailbox>.Decorate(IMailbox value)
            {
                this.value = value;
            }

            ValueTask<IContext> IMailbox.ReceiveAsync(CancellationToken token)
            {
                return onReceive != null ? onReceive(value.ReceiveAsync, token) : value.ReceiveAsync(token);
            }

            ValueTask IMailbox.SendAsync(IContext message, CancellationToken token)
            {
                return onSend != null ? onSend(value.SendAsync, message, token) : value.SendAsync(message, token);
            }
        }

        private class Instance : IMailbox
        {
            private readonly Func<CancellationToken, ValueTask<IContext>> onReceive;
            private readonly Func<IContext, CancellationToken, ValueTask> onSend;

            public Instance(
                Func<CancellationToken, ValueTask<IContext>> onReceive,
                Func<IContext, CancellationToken, ValueTask> onSend)
            {
                this.onReceive = onReceive;
                this.onSend = onSend;
            }

            ValueTask<IContext>  IMailbox.ReceiveAsync(CancellationToken token)
            {
                return onReceive != null ? onReceive(token) : default;
            }

            ValueTask IMailbox.SendAsync(IContext message, CancellationToken token)
            {
                return onSend != null ? onSend(message, token) : default;
            }
        }
    }
}