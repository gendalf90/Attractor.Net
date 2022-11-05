namespace Attractor
{
    public interface IContext : ICloneable<IContext>
    {
        void Set<T>(T value) where T : class;

        T Get<T>() where T : class;
    }
}
