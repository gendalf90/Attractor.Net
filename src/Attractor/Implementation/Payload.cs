namespace Attractor.Implementation
{
    public static class Payload
    {
        private static readonly EmptyPayload empty = new();

        public static IPayload FromString(string value)
        {
            return new TypedPayload<string>(value);
        }

        public static IPayload FromBytes(params byte[] value)
        {
            return new TypedPayload<byte[]>(value);
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
    }
}