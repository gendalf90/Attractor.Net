namespace Attractor
{
    public interface IStreamHandlerDecorator : IStreamHandler
    {
        void Decorate(IStreamHandler handler);
    }
}
