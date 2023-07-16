using Attractor.Implementation;

namespace Attractor
{
    public interface IActorProcess : IActorRef
    {
        void Stop();
    }
}
