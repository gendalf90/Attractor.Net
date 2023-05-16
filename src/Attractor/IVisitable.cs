namespace Attractor
{
    public interface IVisitable
    {
        void Accept<T>(T visitor) where T : IVisitor;
    }
}
