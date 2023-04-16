namespace Attractor.Implementation
{
    internal sealed class TypedBuffer<T> : IPayload
    {
        private readonly T value;

        public TypedBuffer(T value)
        {
            this.value = value;
        }

        void IVisitable.Accept<TVisitor>(TVisitor visitor)
        {
            visitor.Visit(value);
        }
    }
}
