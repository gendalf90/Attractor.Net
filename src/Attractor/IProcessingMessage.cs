using System;

namespace Attractor
{
    public interface IProcessingMessage : IAddress, IMetadata, IAsyncDisposable
    {
    }
}
