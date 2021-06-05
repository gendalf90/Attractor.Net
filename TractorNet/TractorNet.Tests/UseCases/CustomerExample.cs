using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Threading.Channels;
using System.Linq;
using System.Collections.Generic;

namespace TractorNet.Tests.UseCases
{
    public class CustomerExample
    {
        private class CustomerActor : IActor
        {
            private readonly Channel<string> resultsChannel;

            public CustomerActor(Channel<string> resultsChannel)
            {
                this.resultsChannel = resultsChannel;
            }

            public async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                var message = context.Metadata.GetFeature<IReceivedMessageFeature>();
                var customerId = GetId(message);

                await resultsChannel.Writer.WriteAsync(customerId, token);
                await message.ConsumeAsync(token);
            }

            private string GetId(IAddress address)
            {
                return TestStringAddress.ToString(address).Split('/').Last();
            }
        }

        [Fact]
        public async Task Run()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(resultsChannel);

                    services.AddTractor(tractorBuilder =>
                    {
                        tractorBuilder.RegisterActor<CustomerActor>(actorBuilder =>
                        {
                            actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("actors/customer/"));
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("actors/customer/1"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("actors/customer/2"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("actors/customer/3"), Mock.Of<IPayload>());

            // Assert
            var ids = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                ids.Add(await resultsChannel.Reader.ReadAsync());
            }

            Assert.Contains("1", ids);
            Assert.Contains("2", ids);
            Assert.Contains("3", ids);

            await host.StopAsync();
        }
    }
}
