namespace Attractor;

public interface IContextBuilder
{
    void Set<T>(T value) where T : class;
}
