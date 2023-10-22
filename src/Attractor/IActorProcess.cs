namespace Attractor
{
    public interface IActorProcess : IActorRef
    {
        bool IsStarting();

        bool IsProcessing();

        bool IsStopping();

        bool IsCollecting();
        
        void Stop();
    }
}
