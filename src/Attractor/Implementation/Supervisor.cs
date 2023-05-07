using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Supervisor
    {
        public static ISupervisor FromStrategy(
            Func<IContext, Exception, CancellationToken, ValueTask> onProcessed = null,
            Func<IContext, CancellationToken, ValueTask> onStopped = null)
        {
            return new StrategySupervisor(onProcessed, onStopped);
        }

        private record StrategySupervisor(
            Func<IContext, Exception, CancellationToken, ValueTask> OnProcessed = null,
            Func<IContext, CancellationToken, ValueTask> OnStopped = null) : ISupervisor
        {
            ValueTask ISupervisor.OnProcessedAsync(IContext context, Exception error, CancellationToken token)
            {
                return OnProcessed != null ? OnProcessed(context, error, token) : default;
            }

            ValueTask ISupervisor.OnStoppedAsync(IContext context, CancellationToken token)
            {
                return OnStopped != null ? OnStopped(context, token) : default;
            }
        }
    }
}