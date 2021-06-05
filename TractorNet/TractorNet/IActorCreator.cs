using System;

namespace TractorNet
{
    internal interface IActorCreator : IAsyncDisposable
    {
        IActor Create();
    }
}
