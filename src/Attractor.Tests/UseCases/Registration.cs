using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Attractor.Tests.UseCases
{
    public class Registration
    {
        // examples of how actors can be registered
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

                    // register attractor services
                    // in memory mailbox and address book are used by default
                    services.AddAttractorServer();

                    // register the actor with scoped lifetime (will be created for each message)
                    services.RegisterActor<TestActor>(actorBuilder =>
                    {
                        // register address policy with singleton lifetime
                        // it is used for matching each message address with concrette actor class
                        actorBuilder.UseAddressPolicy<AbcAddressPolicy>();

                        // auto consume message after processing so you don't need to get the message feature for manual consuming
                        actorBuilder.UseAutoConsume();
                    });

                    services.RegisterActor(provider =>
                    {
                        return new TestActor(provider.GetRequiredService<CountdownEvent>());
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => new StringAddressPolicy("def"));
                        actorBuilder.UseAutoConsume();
                    });

                    services.RegisterActor((context, token) =>
                    {
                        completionTrigger.Signal();
                    }, actorBuilder =>
                    {
                        actorBuilder.UseBatching();

                        // addresses can be different so if they are matching by policy
                        // different instances of the actor class will be created (for each unique address)
                        actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("ghi"));
                        actorBuilder.UseAutoConsume();
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            // there are three actors with different types are created
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("abc"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("def"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("ghij"), Mock.Of<IPayload>());

            var completionStatus = await Task.Run(() => completionTrigger.Wait(completionTimeout));

            await host.StopAsync();

            // Assert
            Assert.True(completionStatus);
        }

        private class TestActor : IActor
        {
            private CountdownEvent completionTrigger;

            public TestActor(CountdownEvent completionTrigger)
            {
                this.completionTrigger = completionTrigger;
            }

            public ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                completionTrigger.Signal();

                return new ValueTask(Task.CompletedTask);
            }
        }

        private class AbcAddressPolicy : IAddressPolicy
        {
            public ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default)
            {
                return new ValueTask<bool>(Task.FromResult(TestStringAddress.ToString(address) == "abc"));
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
                return new ValueTask<bool>(Task.FromResult(TestStringAddress.ToString(address) == value));
            }
        }
    }
}
