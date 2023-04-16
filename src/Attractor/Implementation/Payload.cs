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

        public static IPayload Empty()
        {
            return instance;
        }

        private class EmptyBuffer : IPayload
        {
            void IVisitable.Accept<T>(T visitor)
            {
            }
        }
    }
}