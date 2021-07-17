using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using System.Collections.Generic;
using Moq;
using System.Linq;

namespace TractorNet.Tests.UseCases
{
    // represents current actor and its methods
    public class SelfFeature
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
                        var self = context.Metadata.GetFeature<ISelfFeature>();
                        var message = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(TestStringAddress.ToString(self));
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
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address/2"), Mock.Of<IPayload>());
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address/3"), Mock.Of<IPayload>());

            var results = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                results.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.Contains("address/1", results);
            Assert.Contains("address/2", results);
            Assert.Contains("address/3", results);
        }

        [Fact]
        public async Task SendMessageToOtherActor()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var self = context.Metadata.GetFeature<ISelfFeature>();
                        var message = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        if (TestStringAddress.ToString(message) == "address/1")
                        {
                            // in difference of the anonymous outbox the sent message will have a 'from' address
                            // and therefore a 'sender' feature will be available in a receiving actor
                            await self.SendMessageAsync(TestStringAddress.CreateAddress("address/2"), TestStringPayload.Create("payload/2"));
                        }
                        
                        await resultsChannel.Writer.WriteAsync(TestStringAddress.ToString(message));
                        await resultsChannel.Writer.WriteAsync(TestStringPayload.ToString(message));
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

            for (int i = 0; i < 4; i++)
            {
                results.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.Contains("address/1", results);
            Assert.Contains("address/2", results);
            Assert.Contains("payload/1", results);
            Assert.Contains("payload/2", results);
        }

        [Fact]
        public async Task SendMessageToSelfActor()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var self = context.Metadata.GetFeature<ISelfFeature>();
                        var message = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        if (TestStringPayload.ToString(message) == "payload/1")
                        {
                            // like the sending method mentioned above
                            // but with current actor address as parameter
                            await self.SendMessageAsync(TestStringPayload.Create("payload/2"));
                        }

                        await resultsChannel.Writer.WriteAsync(TestStringAddress.ToString(message));
                        await resultsChannel.Writer.WriteAsync(TestStringPayload.ToString(message));
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

            for (int i = 0; i < 4; i++)
            {
                results.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.Equal(2, results.Count(result => result == "address/1"));
            Assert.Contains("payload/1", results);
            Assert.Contains("payload/2", results);
        }
    }
}
