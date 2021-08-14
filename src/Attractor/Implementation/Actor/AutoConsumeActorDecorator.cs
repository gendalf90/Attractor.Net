using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation.Actor
{
    internal sealed class AutoConsumeActorDecorator : BaseActorDecorator
    {
        public override async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
        {
            await actor.OnReceiveAsync(context, token);

            var messageFeature = context.Metadata.GetFeature<IReceivedMessageFeature>();

            if (messageFeature != null)
            {
                await messageFeature.ConsumeAsync(token);
            }
        }
    }
}
