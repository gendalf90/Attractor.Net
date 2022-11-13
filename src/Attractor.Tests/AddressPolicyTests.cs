using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Attractor.Implementation;

namespace Attractor.Tests
{
    public class AddressPolicyTests
    {
        private class TestStringAddressPolicy : IAddressPolicy
        {
            private readonly IAddress _address;

            public TestStringAddressPolicy(string address)
            {
                _address = StringBuffer.Address(address);
            }

            public bool IsMatch(IAddress address)
            {
                return _address.GetEquatable().Equals(address);
            }
        }

        [Fact]
        public async Task MapStream_UseCustomRegistered_MessageIsReceived()
        {
            // Arrange
            var isReceived = false;

            var services = new ServiceCollection();

            services.AddSystem();
            services.AddSingleton(new TestStringAddressPolicy("address"));
            services.MapStream(provider => provider.GetService<TestStringAddressPolicy>(), builder =>
            {
                builder.Chain(onReceive: _ => isReceived = true);
            });

            // Act
            var system = services.BuildServiceProvider().GetService<ISystem>();

            await using (var reference = await system.UseAsync(StringBuffer.Address("address")))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

            // Assert
            Assert.True(isReceived);
        }

        [Fact]
        public async Task MapStream_UseStrategy_MessageIsReceived()
        {
            // Arrange
            var isReceived = false;

            var services = new ServiceCollection();

            services.AddSystem();
            services.MapStream(address => StringBuffer.Address("address").GetEquatable().Equals(address), builder =>
            {
                builder.Chain(onReceive: _ => isReceived = true);
            });

            // Act
            var system = services.BuildServiceProvider().GetService<ISystem>();

            await using (var reference = await system.UseAsync(StringBuffer.Address("address")))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

            // Assert
            Assert.True(isReceived);
        }

        [Fact]
        public async Task MapStream_UseCustomInstance_MessageIsReceived()
        {
            // Arrange
            var isReceived = false;

            var services = new ServiceCollection();

            services.AddSystem();
            services.MapStream(new TestStringAddressPolicy("address"), builder =>
            {
                builder.Chain(onReceive: _ => isReceived = true);
            });

            // Act
            var system = services.BuildServiceProvider().GetService<ISystem>();

            await using (var reference = await system.UseAsync(StringBuffer.Address("address")))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

            // Assert
            Assert.True(isReceived);
        }

        [Fact]
        public async Task MapStream_UseBytes_MessageIsReceived()
        {
            // Arrange
            var addressBytes = new byte[] { 1, 2, 3 };

            var isReceived = false;

            var services = new ServiceCollection();

            services.AddSystem();
            services.MapStream(BytesBuffer.Policy(addressBytes), builder =>
            {
                builder.Chain(onReceive: _ => isReceived = true);
            });

            // Act
            var system = services.BuildServiceProvider().GetService<ISystem>();

            await using (var reference = await system.UseAsync(BytesBuffer.Address(addressBytes)))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

            // Assert
            Assert.True(isReceived);
        }

        [Fact]
        public async Task MapStream_UseBytesWithPattern_MessageIsReceived()
        {
            // Arrange
            var isReceived = false;

            var services = new ServiceCollection();

            services.AddSystem();
            services.MapStream(BytesBuffer.Policy(bytes => !bytes.IsEmpty && bytes.Span[0] == 10), builder =>
            {
                builder.Chain(onReceive: _ => isReceived = true);
            });

            // Act
            var system = services.BuildServiceProvider().GetService<ISystem>();

            await using (var reference = await system.UseAsync(BytesBuffer.Address(new byte[] { 10, 1, 8})))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

            // Assert
            Assert.True(isReceived);
        }

        [Fact]
        public async Task MapStream_UseStringWithPattern_MessageIsReceived()
        {
            // Arrange
            var isReceived = false;

            var services = new ServiceCollection();

            services.AddSystem();
            services.MapStream(StringBuffer.Policy(str => str.StartsWith("10")), builder =>
            {
                builder.Chain(onReceive: _ => isReceived = true);
            });

            // Act
            var system = services.BuildServiceProvider().GetService<ISystem>();

            await using (var reference = await system.UseAsync(StringBuffer.Address("10.1.8")))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

            // Assert
            Assert.True(isReceived);
        }
    }
}
