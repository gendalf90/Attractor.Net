using System;
using System.Text;

namespace Attractor.Tests
{
    public class TestStringState : IState
    {
        private readonly string value;

        private TestStringState(string value)
        {
            this.value = value;
        }

        public ReadOnlyMemory<byte> GetBytes()
        {
            return Encoding.UTF8.GetBytes(value);
        }

        public static IState Create(string value)
        {
            return new TestStringState(value);
        }

        public static string ToString(IState payload)
        {
            return Encoding.UTF8.GetString(payload.GetBytes().Span);
        }
    }
}
