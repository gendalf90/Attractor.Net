namespace Attractor
{
    public interface IDecorator<T>
    {
        void Decorate(T value);
    }
}