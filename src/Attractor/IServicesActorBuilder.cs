using System;
using Microsoft.Extensions.DependencyInjection;

namespace Attractor
{
    public interface IServicesActorBuilder : IActorBuilder
    {
        IServiceCollection Services { get; }

        void RegisterActor<T>(Func<IServiceProvider, T> factory) where T : class, IActor;

        void RegisterMailbox<T>(Func<IServiceProvider, T> factory) where T : class, IMailbox;

        void RegisterSupervisor<T>(Func<IServiceProvider, T> factory) where T : class, ISupervisor;
        
        void DecorateActor<T>(Func<IServiceProvider, T> factory) where T : class, IActorDecorator;

        void DecorateMailbox<T>(Func<IServiceProvider, T> factory) where T : class, IMailboxDecorator;

        void DecorateSupervisor<T>(Func<IServiceProvider, T> factory) where T : class, ISupervisorDecorator;
    }
}