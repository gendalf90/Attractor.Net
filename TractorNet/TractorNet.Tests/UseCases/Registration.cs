using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace TractorNet.Tests.UseCases
{
    public class Registration
    {
        private class TestActor : IActor
        {
            private CountdownEvent completionTrigger;

            public TestActor(CountdownEvent completionTrigger)
            {
                this.completionTrigger = completionTrigger;
            }

            public async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                await context
                    .Metadata
                    .GetFeature<IReceivedMessageFeature>()
                    .ConsumeAsync();

                completionTrigger.Signal();
            }
        }

        private class AbcAddressPolicy : IAddressPolicy
        {
            public ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default)
            {
                return ValueTask.FromResult(TestStringAddress.ToString(address) == "abc");
            }
        }

        private class StringAddressPolicy : IAddressPolicy
        {
            private readonly string value;

            public StringAddressPolicy(string value)
            {
                this.value = value;
            }

            public ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default)
            {
                return ValueTask.FromResult(TestStringAddress.ToString(address) == value);
            }
        }

        [Fact]
        public async Task Run()
        {
            // Arrange
            var completionTimeout = TimeSpan.FromSeconds(10);
            using var completionTrigger = new CountdownEvent(3);

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(completionTrigger);
                    services.AddTractor();

                    services.RegisterActor<TestActor>(actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy<AbcAddressPolicy>();
                    });

                    services.RegisterActor(provider =>
                    {
                        return new TestActor(provider.GetRequiredService<CountdownEvent>());
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => new StringAddressPolicy("def"));
                    });

                    services.RegisterActor(async (context, token) =>
                    {
                        await context
                            .Metadata
                            .GetFeature<IReceivedMessageFeature>()
                            .ConsumeAsync();

                        completionTrigger.Signal();
                    }, actorBuilder =>
                    {
                        actorBuilder.UseBatching();
                        actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("ghi"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("abc"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("def"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("ghij"), Mock.Of<IPayload>());

            var completionStatus = await Task.Run(() => completionTrigger.Wait(completionTimeout));

            await host.StopAsync();

            // Assert
            Assert.True(completionStatus);
        }
    }
}
