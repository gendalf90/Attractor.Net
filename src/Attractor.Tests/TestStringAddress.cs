using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Tests
{
    public class TestStringAddress : IAddress, IAddressPolicy
    {
        private readonly string value;

        private TestStringAddress(string value)
        {
            this.value = value;
        }

        public ReadOnlyMemory<byte> GetBytes()
        {
            return Encoding.UTF8.GetBytes(value);
        }

        public ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default)
        {
            return ValueTask.FromResult(ToString(address) == value);
        }

        public static IAddressPolicy CreatePolicy(string toMatchValue)
        {
            return new TestStringAddress(toMatchValue);
        }

        public static IAddress CreateAddress(string value)
        {
            return new TestStringAddress(value);
        }

        public static string ToString(IAddress address)
        {
            return Encoding.UTF8.GetString(address.GetBytes().Span);
        }
    }
}
