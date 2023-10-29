namespace Attractor
{
    public interface IActorProcess : IActorRef
    {
        bool IsStarting();

        bool IsActive();

        bool IsStopping();

        bool IsCollecting();
        
        void Stop();
    }
}
