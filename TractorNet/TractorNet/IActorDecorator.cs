namespace TractorNet
{
    public interface IActorDecorator : IActor
    {
        void Decorate(IActor actor);
    }
}
