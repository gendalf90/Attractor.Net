namespace Attractor.Implementation.Common
{
    internal static class ActorDecoratorExtensions
    {
        public static T Wrap<T>(this T decorator, IActor actor) where T : IActorDecorator
        {
            decorator.Decorate(actor);

            return decorator;
        }
    }
}
