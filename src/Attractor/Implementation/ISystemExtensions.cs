using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class ISystemExtensions
    {
        public static async ValueTask PostAsync(this ISystem system, IAddress address, IPayload payload, Action<IContext> configuration = null, CancellationToken token = default)
        {
            await using (var reference = await system.UseAsync(address, token))
            {
                await reference.SendAndWaitAsync(payload, configuration, token);
            }
        }

        public static async ValueTask<UsableRef> UseAsync(this ISystem system, IAddress address, CancellationToken token = default)
        {
            var reference = await system.GetAsync(address, token);

            return new UsableRef(reference);
        }

        public sealed class UsableRef : IRef, IAsyncDisposable
        {
            private readonly IRef baseRef;

            public UsableRef(IRef baseRef)
            {
                this.baseRef = baseRef;
            }

            public void Accept(IVisitor visitor)
            {
                baseRef.Accept(visitor);
            }

            public void Cancel()
            {
                baseRef.Cancel();
            }

            public IAddress Clone()
            {
                return baseRef.Clone();
            }

            public IEquatable<IAddress> GetEquatable()
            {
                return baseRef.GetEquatable();
            }

            public CancellationToken GetToken()
            {
                return baseRef.GetToken();
            }

            public ValueTask<IRequest> SendAsync(IPayload payload, Action<IContext> configuration = null, CancellationToken token = default)
            {
                return baseRef.SendAsync(payload, configuration, token);
            }

            public Task WaitAsync(CancellationToken token = default)
            {
                return baseRef.WaitAsync(token);
            }

            public async ValueTask DisposeAsync()
            {
                baseRef.Cancel();

                await baseRef.WaitAsync();
            }
        }
    }
}
