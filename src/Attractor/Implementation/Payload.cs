namespace Attractor.Implementation
{
    public static class Payload
    {
        private static readonly EmptyPayload empty = new();

        public static IPayload From<T>(T value)
        {
            return new TypedPayload<T>(value);
        }

        public static bool Match<T>(IPayload payload)
        {
            var visitor = new MatchTypeVisitor<T>();
            
            payload.Accept(visitor);

            return visitor.IsMatch;
        }

        public static IPayload Empty()
        {
            return empty;
        }

        private class EmptyPayload : IPayload
        {
            void IVisitable.Accept<T>(T visitor)
            {
            }
        }

        private class TypedPayload<TType> : IPayload
        {
            private readonly TType value;

            public TypedPayload(TType value)
            {
                this.value = value;
            }

            void IVisitable.Accept<TVisitor>(TVisitor visitor)
            {
                visitor.Visit(value);
            }
        }

        private class MatchTypeVisitor<TType> : IVisitor
        {
            public void Visit<TValue>(TValue value)
            {
                if (typeof(TValue) == typeof(TType))
                {
                    IsMatch = true;
                }
            }

            public bool IsMatch { get; private set; }
        }
    }
}