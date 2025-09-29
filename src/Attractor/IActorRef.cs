using System;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorRef : IDisposable
    {
        Task Send(IMessage message);

        void OnComplete(Action action);
    }
}
