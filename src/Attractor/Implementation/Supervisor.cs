using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Supervisor
    {
        private static readonly Stub stub = new();

        public static ISupervisorDecorator FromStrategy(
            Func<Func<IContext, Exception, CancellationToken, ValueTask>, IContext, Exception, CancellationToken, ValueTask> onFault = null)
        {
            return new Decorator(onFault);
        }

        public static ISupervisor FromStrategy(
            Func<IContext, Exception, CancellationToken, ValueTask> onFault = null)
        {
            return new Instance(onFault);
        }

        public static ISupervisor Empty()
        {
            return stub;
        }

        private class Decorator : ISupervisorDecorator
        {
            private readonly Func<Func<IContext, Exception, CancellationToken, ValueTask>, IContext, Exception, CancellationToken, ValueTask> onFault;

            private ISupervisor value;

            public Decorator(
                Func<Func<IContext, Exception, CancellationToken, ValueTask>, IContext, Exception, CancellationToken, ValueTask> onFault)
            {
                this.onFault = onFault;
            }

            void IDecorator<ISupervisor>.Decorate(ISupervisor value)
            {
                this.value = value;
            }

            ValueTask ISupervisor.OnFaultAsync(IContext context, Exception exception, CancellationToken token)
            {
                return onFault != null ? onFault(value.OnFaultAsync, context, exception, token) : value.OnFaultAsync(context, exception, token);
            }
        }

        private class Instance : ISupervisor
        {
            private readonly Func<IContext, Exception, CancellationToken, ValueTask> onFault;

            public Instance(
                Func<IContext, Exception, CancellationToken, ValueTask> onFault)
            {
                this.onFault = onFault;
            }

            ValueTask ISupervisor.OnFaultAsync(IContext context, Exception exception, CancellationToken token)
            {
                return onFault != null ? onFault(context, exception, token) : default;
            }
        }
        
        private class Stub : ISupervisor
        {
            ValueTask ISupervisor.OnFaultAsync(IContext context, Exception exception, CancellationToken token)
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}