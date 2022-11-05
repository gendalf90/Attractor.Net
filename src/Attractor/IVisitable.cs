namespace Attractor
{
    public interface IVisitable
    {
        void Accept(IVisitor visitor);
    }
}
