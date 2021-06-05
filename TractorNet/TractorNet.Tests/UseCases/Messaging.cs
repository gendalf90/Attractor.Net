using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace TractorNet.Tests.UseCases
{
    public class Messaging
    {
        private const int MaxRunningNumberForTheSameAddress = 1;

        private int currentRunningNumber = 0;

        private readonly Channel<int> resultChannel = Channel.CreateUnbounded<int>();

        private async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token)
        {
            await resultChannel.Writer.WriteAsync(Interlocked.Increment(ref currentRunningNumber));

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            await context
                .Metadata
                .GetFeature<IReceivedMessageFeature>()
                .ConsumeAsync();

            Interlocked.Decrement(ref currentRunningNumber);
        }

        [Fact]
        public async Task Run()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor(tractorBuilder =>
                    {
                        tractorBuilder.RegisterActor(OnReceiveAsync, actorBuilder =>
                        {
                            actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("123"));
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            for (int i = 0; i < 10; i++)
            {
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress("123"), Mock.Of<IPayload>());
            }

            // Assert
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(MaxRunningNumberForTheSameAddress, await resultChannel.Reader.ReadAsync());
            }

            await host.StopAsync();
        }

        [Fact]
        public async Task RunWithBatching()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor(tractorBuilder =>
                    {
                        tractorBuilder.RegisterActor(OnReceiveAsync, actorBuilder =>
                        {
                            actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("123"));
                            actorBuilder.UseBatching();
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            for (int i = 0; i < 10; i++)
            {
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress("123"), Mock.Of<IPayload>());
            }

            // Assert
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(MaxRunningNumberForTheSameAddress, await resultChannel.Reader.ReadAsync());
            }

            await host.StopAsync();
        }
    }
}
