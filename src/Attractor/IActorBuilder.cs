using System;

namespace Attractor
{
    public interface IActorBuilder
    {
        void RegisterActor<T>(Func<T> factory) where T : class, IActor;

        void RegisterMailbox<T>(Func<T> factory) where T : class, IMailbox;

        void RegisterSupervisor<T>(Func<T> factory) where T : class, ISupervisor;
        
        void DecorateActor<T>(Func<T> factory) where T : class, IActorDecorator;

        void DecorateMailbox<T>(Func<T> factory) where T : class, IMailboxDecorator;

        void DecorateSupervisor<T>(Func<T> factory) where T : class, ISupervisorDecorator;
    }
}