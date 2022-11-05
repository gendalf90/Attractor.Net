using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class IRefExtensions
    {
        public static async ValueTask SendAndWaitAsync(this IRef reference, IPayload payload, Action<IContext> configuration = null, CancellationToken token = default)
        {
            var request = await reference.SendAsync(payload, configuration, token);

            await request.WaitAsync(token);
        }
    }
}
