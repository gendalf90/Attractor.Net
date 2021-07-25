using System;

namespace Attractor
{
    internal interface IActorCreator : IAsyncDisposable
    {
        IActor Create();
    }
}
