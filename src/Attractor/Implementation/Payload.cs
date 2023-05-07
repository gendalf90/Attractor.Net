using System;

namespace Attractor.Implementation
{
    public static class Payload
    {
        private static readonly EmptyBuffer instance = new();
        
        public static IPayload FromString(string value)
        {
            return new StringBuffer(value);
        }

        public static IPayload FromBytes(ReadOnlyMemory<byte> value)
        {
            return new BytesBuffer(value);
        }

        public static IPayload FromType<T>(T value)
        {
            return new TypedBuffer<T>(value);
        }

        public static bool MatchType<T>(IPayload payload)
        {
            var result = payload.Accept(new MatchTypeVisitor<T>());

            return result.IsMatch;
        }

        public static IPayload Empty()
        {
            return instance;
        }

        private class EmptyBuffer : IPayload
        {
            T IVisitable.Accept<T>(T visitor)
            {
                return visitor;
            }
        }

        private class TypedBuffer<T> : IPayload
        {
            private readonly T value;

            public TypedBuffer(T value)
            {
                this.value = value;
            }

            TVisitor IVisitable.Accept<TVisitor>(TVisitor visitor)
            {
                visitor.Visit(value);

                return visitor;
            }
        }

        private struct MatchTypeVisitor<TType> : IVisitor
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