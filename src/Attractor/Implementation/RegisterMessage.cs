using System;
using System.Threading;

namespace Attractor.Implementation;

internal class RegisterMessage(IAddressPolicy addressPolicy, ActorBuilder actorBuilder)
{
    public bool IsMatch(IAddress address)
    {
        return addressPolicy.IsMatch(address);
    }

    public Process Build(IAddress address, IAsyncDisposable disposing, CancellationToken token)
    {
        var actor = actorBuilder.Build();

        return new Process(address, actor, disposing, token);
    }
}