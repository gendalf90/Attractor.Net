namespace Attractor
{
    public interface IVisitor
    {
        void Visit<T>(T value);
    }
}
