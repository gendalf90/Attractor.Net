namespace Attractor.Implementation
{
    public static class Payload
    {
        private static readonly EmptyPayload empty = new();

        public static IPayload From<T>(T value)
        {
            return new TypedPayload<T>(value);
        }

        public static IPayload Combine(params IPayload[] payloads)
        {
            return new CombinedPayload(payloads);
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

        private record CombinedPayload(IPayload[] Payloads) : IPayload
        {
            void IVisitable.Accept<TVisitor>(TVisitor visitor)
            {
                foreach (var payload in Payloads)
                {
                    payload.Accept(visitor);
                }
            }
        }
    }
}