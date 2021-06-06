using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace TractorNet.Tests.UseCases
{
    public class RunningNumberLimit
    {
        private const int RunningLimit = 2;

        private int currentRunningNumber = 0;

        private async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token)
        {
            Interlocked.Increment(ref currentRunningNumber);

            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }

        [Fact]
        public async Task RunWithCommonLimit()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor(tractorBuilder =>
                    {
                        // unbounded by default
                        tractorBuilder.UseRunningNumberLimit(RunningLimit);
                    });

                    services.RegisterActor(OnReceiveAsync, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("123"));
                    });

                    services.RegisterActor(OnReceiveAsync, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("456"));
                    });

                    services.RegisterActor(OnReceiveAsync, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("789"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("123"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("456"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("789"), Mock.Of<IPayload>());

            await Task.Delay(1000);

            // Assert
            await host.StopAsync();

            Assert.Equal(RunningLimit, currentRunningNumber);
        }

        [Fact]
        public async Task RunWithSpecificLimit()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor();
                    services.RegisterActor(OnReceiveAsync, actorBuilder =>
                    {
                        // unbounded by default
                        actorBuilder.UseRunningNumberLimit(RunningLimit);
                        actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("123"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1234"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1235"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1236"), Mock.Of<IPayload>());

            await Task.Delay(1000);

            // Assert
            await host.StopAsync();

            Assert.Equal(RunningLimit, currentRunningNumber);
        }

        [Fact]
        public async Task RunWithSpecificLimitWithBatching()
        {
            // Arrange
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractor();
                    services.RegisterActor(OnReceiveAsync, actorBuilder =>
                    {
                        // unbounded by default
                        actorBuilder.UseRunningNumberLimit(RunningLimit);
                        actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("123"));
                        actorBuilder.UseBatching();
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1234"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1235"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("1236"), Mock.Of<IPayload>());

            await Task.Delay(1000);

            // Assert
            await host.StopAsync();

            Assert.Equal(RunningLimit, currentRunningNumber);
        }
    }
}
