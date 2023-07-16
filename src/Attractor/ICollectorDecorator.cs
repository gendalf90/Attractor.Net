namespace Attractor
{
    public interface ICollectorDecorator : ICollector, IDecorator<ICollector>
    {
    }
}
