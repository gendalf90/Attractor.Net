using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

public static class Extensions
{
    public static async Task SendAsync(this IActorRef actor, IMessage message, CancellationToken token = default)
    {
        var request = actor.Send(message, token);

        await request.Completion;
    }
}
