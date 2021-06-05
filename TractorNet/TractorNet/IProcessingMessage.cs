using System;

namespace TractorNet
{
    public interface IProcessingMessage : IAddress, IMetadata, IAsyncDisposable
    {
    }
}
