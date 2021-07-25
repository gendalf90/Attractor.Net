using System;
using System.Text;

namespace Attractor.Tests
{
    public class TestStringPayload : IPayload
    {
        private readonly string value;

        private TestStringPayload(string value)
        {
            this.value = value;
        }

        public ReadOnlyMemory<byte> GetBytes()
        {
            return Encoding.UTF8.GetBytes(value);
        }

        public static IPayload Create(string value)
        {
            return new TestStringPayload(value);
        }

        public static string ToString(IPayload payload)
        {
            return Encoding.UTF8.GetString(payload.GetBytes().Span);
        }
    }
}
