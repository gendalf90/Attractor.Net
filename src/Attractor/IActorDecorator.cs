namespace Attractor
{
    public interface IActorDecorator : IActor
    {
        void Decorate(IActor actor);
    }
}
