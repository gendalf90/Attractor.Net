namespace Attractor.Implementation
{
    public static class Payload
    {
        private static readonly EmptyPayload empty = new();

        public static IPayload FromType<T>(T value)
        {
            return new TypedPayload<T>(value);
        }

        public static IPayload Empty()
        {
            return empty;
        }

        private record EmptyPayload : IPayload
        {
            void IVisitable.Accept<T>(T visitor)
            {
            }
        }

        private record TypedPayload<TPayload>(TPayload Value) : IPayload
        {
            void IVisitable.Accept<TVisitor>(TVisitor visitor)
            {
                visitor.Visit(Value);
            }
        }
    }
}