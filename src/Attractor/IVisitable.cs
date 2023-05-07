namespace Attractor
{
    public interface IVisitable
    {
        T Accept<T>(T visitor) where T : IVisitor;
    }
}
