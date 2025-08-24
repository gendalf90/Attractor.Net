namespace Attractor
{
    public interface IContext
    {
        T Get<T>() where T : class;
    }
}
