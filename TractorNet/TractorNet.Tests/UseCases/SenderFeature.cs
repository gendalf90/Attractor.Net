using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using System.Collections.Generic;
using Moq;

namespace TractorNet.Tests.UseCases
{
    // represents sender actor and its methods
    public class SenderFeature
    {
        [Fact]
        public async Task UseAddress()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var sender = context.Metadata.GetFeature<ISenderFeature>();
                        var self = context.Metadata.GetFeature<ISelfFeature>();
                        var message = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        // the sender is null when a message was sent by anonymous outbox
                        if (sender == null)
                        {
                            await self.SendMessageAsync(TestStringAddress.CreateAddress("address/2"), Mock.Of<IPayload>());
                        }
                        else
                        {
                            await resultsChannel.Writer.WriteAsync(TestStringAddress.ToString(sender));
                        }

                        await message.ConsumeAsync();
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address/1"), Mock.Of<IPayload>());

            var senderAddress = await resultsChannel.Reader.ReadAsync();

            await host.StopAsync();

            // Assert
            Assert.Equal("address/1", senderAddress);
        }

        [Fact]
        public async Task SendMessageToSender()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var sender = context.Metadata.GetFeature<ISenderFeature>();
                        var self = context.Metadata.GetFeature<ISelfFeature>();
                        var message = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        // at first receiving from anonymous outbox the sender is null in the actor 1
                        if (sender == null)
                        {
                            // send message to actor 2 from actor 1
                            await self.SendMessageAsync(TestStringAddress.CreateAddress("address/2"), Mock.Of<IPayload>());
                        }
                        // at the second receiving the sender is not null in the actor 2 so the message was sent from actor 1
                        else if (TestStringAddress.ToString(self) == "address/2")
                        {
                            // send message back to actor 1
                            await sender.SendMessageAsync(TestStringPayload.Create("payload to response a sender"));
                        }
                        // at third receiving the sender is not null in actor 1 so the message was sent from actor 2
                        else
                        {
                            await resultsChannel.Writer.WriteAsync(TestStringAddress.ToString(message));
                            await resultsChannel.Writer.WriteAsync(TestStringPayload.ToString(message));
                        }

                        await message.ConsumeAsync();
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy((address, token) => TestStringAddress.ToString(address).StartsWith("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address/1"), TestStringPayload.Create("payload/1"));

            var results = new List<string>();

            for (int i = 0; i < 2; i++)
            {
                results.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.Contains("address/1", results);
            Assert.Contains("payload to response a sender", results);
        }
    }
}
