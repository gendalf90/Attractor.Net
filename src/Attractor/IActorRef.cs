namespace Attractor
{
    public interface IActorRef
    {
        void Send(IMessage message);
    }
}
