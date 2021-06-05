using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace TractorNet.Tests.UseCases
{
    public class LaunchTrottleTime
    {
        private static readonly TimeSpan trottleTime = TimeSpan.FromMilliseconds(100);

        private int launches = 0;

        private async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token)
        {
            Interlocked.Increment(ref launches);

            await context
                .Metadata
                .GetFeature<IReceivedMessageFeature>()
                .ConsumeAsync();
        }

        [Fact]
        public async Task RunWithCommonTrottleTime()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor(tractorBuilder =>
                    {
                        tractorBuilder.UseLaunchTrottleTime(trottleTime);

                        tractorBuilder.RegisterActor(OnReceiveAsync, actorBuilder =>
                        {
                            actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("123"));
                        });

                        tractorBuilder.RegisterActor(OnReceiveAsync, actorBuilder =>
                        {
                            actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("456"));
                        });

                        tractorBuilder.RegisterActor(OnReceiveAsync, actorBuilder =>
                        {
                            actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("789"));
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
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress("456"), Mock.Of<IPayload>());
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress("789"), Mock.Of<IPayload>());
            }

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(2));

            await host.StopAsync();

            Assert.True(launches <= 20);
        }

        [Fact]
        public async Task RunWithSpecificTrottleTime()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor(tractorBuilder =>
                    {
                        tractorBuilder.RegisterActor(OnReceiveAsync, actorBuilder =>
                        {
                            actorBuilder.UseLaunchTrottleTime(trottleTime);
                            actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("123"));
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            for (int i = 0; i < 10; i++)
            {
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1234"), Mock.Of<IPayload>());
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1235"), Mock.Of<IPayload>());
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1236"), Mock.Of<IPayload>());
            }

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(2));

            await host.StopAsync();

            Assert.True(launches <= 20);
        }

        [Fact]
        public async Task RunWithSpecificTrottleTimeWithBatchingAndDifferentAddresses()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor(tractorBuilder =>
                    {
                        tractorBuilder.RegisterActor(OnReceiveAsync, actorBuilder =>
                        {
                            actorBuilder.UseLaunchTrottleTime(trottleTime);
                            actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("123"));
                            actorBuilder.UseBatching();
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            for (int i = 0; i < 30; i++)
            {
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress($"123{i}"), Mock.Of<IPayload>());
            }

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(2));

            await host.StopAsync();

            Assert.True(launches <= 20);
        }

        [Fact]
        public async Task RunWithSpecificTrottleTimeWithBatchingAndTheSameAddress()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor(tractorBuilder =>
                    {
                        tractorBuilder.RegisterActor(OnReceiveAsync, actorBuilder =>
                        {
                            actorBuilder.UseLaunchTrottleTime(trottleTime);
                            actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("123"));
                            actorBuilder.UseBatching();
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            for (int i = 0; i < 30; i++)
            {
                await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1234"), Mock.Of<IPayload>());
            }

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(1));

            await host.StopAsync();

            // batching actor has already been launched when next message for the same address (1234) is received
            // so trottling is not applied in this case
            Assert.Equal(30, launches);
        }
    }
}
