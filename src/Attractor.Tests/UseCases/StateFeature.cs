using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace Attractor.Tests.UseCases
{
    public class StateFeature
    {
        [Fact]
        public async Task SaveState()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddAttractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var state = context.Metadata.GetFeature<IStateFeature>();
                        var message = context.Metadata.GetFeature<IReceivedMessageFeature>();
                        var currentState = TestStringState.ToString(state);

                        await resultsChannel.Writer.WriteAsync(currentState);

                        var newState = currentState + "test";

                        await state.SaveAsync(TestStringState.Create(newState));
                        await message.ConsumeAsync();
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());

            var results = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                results.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.Contains("", results);
            Assert.Contains("test", results);
            Assert.Contains("testtest", results);
        }

        [Fact]
        public async Task ClearState()
        {
            // Arrange
            var isFirstMessage = true;
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddAttractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var state = context.Metadata.GetFeature<IStateFeature>();
                        var message = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(TestStringState.ToString(state));

                        if (isFirstMessage)
                        {
                            await state.SaveAsync(TestStringState.Create("test"));
                        }
                        else
                        {
                            await state.ClearAsync();
                        }

                        isFirstMessage = false;

                        await message.ConsumeAsync();
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());

            var results = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                results.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.Equal(2, results.Count(result => result == ""));
            Assert.Contains("test", results);
        }
    }
}
