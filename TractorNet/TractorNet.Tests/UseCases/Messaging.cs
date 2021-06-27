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
                    services.AddTractor();
                    services.RegisterActor(OnReceiveAsync, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("123"));
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
                    services.AddTractor();
                    services.RegisterActor(OnReceiveAsync, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("123"));
                        actorBuilder.UseBatching();
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
        public async Task RunWithDelayingMessage()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<DateTime>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor();
                    services.RegisterActor(async (context, token) =>
                    {
                        await resultsChannel.Writer.WriteAsync(DateTime.UtcNow);
                        await context
                            .Metadata
                            .GetFeature<IReceivedMessageFeature>()
                            .ConsumeAsync();
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            var timeOfSending = DateTime.UtcNow;

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>(), new SendingMetadata
            {
                Delay = TimeSpan.FromSeconds(1)
            });

            var timeOfReceiving = await resultsChannel.Reader.ReadAsync();

            await host.StopAsync();

            // Assert
            Assert.True(timeOfReceiving - timeOfSending >= TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task RunWithExpiringMessage()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor();
                    services.RegisterActor(async (context, token) =>
                    {
                        var feature = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(TestStringPayload.ToString(feature));
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), TestStringPayload.Create("payload"), new SendingMetadata
            {
                Delay = TimeSpan.FromSeconds(1),
                Ttl = TimeSpan.FromMilliseconds(500)
            });

            await Task.Delay(TimeSpan.FromSeconds(2));

            await host.StopAsync();

            // Assert
            Assert.False(resultsChannel.Reader.TryRead(out _));
        }
    }
}
