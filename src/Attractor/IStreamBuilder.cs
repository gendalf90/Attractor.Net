using System;

namespace Attractor
{
    public interface IStreamBuilder : IServicesBuilder
    {
        void Decorate<T>(Func<IServiceProvider, T> factory) where T : class, IStreamHandlerDecorator;
    }
}
