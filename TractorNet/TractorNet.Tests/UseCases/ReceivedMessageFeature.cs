using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using System.Collections.Generic;
using Moq;
using System;

namespace TractorNet.Tests.UseCases
{
    // standard feature for received message processing
    public class ReceivedMessageFeature
    {
        [Fact]
        public async Task UseAddressAndPayload()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var feature = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(TestStringAddress.ToString(feature));
                        await resultsChannel.Writer.WriteAsync(TestStringPayload.ToString(feature));

                        await feature.ConsumeAsync();
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
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address/2"), TestStringPayload.Create("payload/2"));
            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address/3"), TestStringPayload.Create("payload/3"));

            var results = new List<string>();

            for (int i = 0; i < 6; i++)
            {
                results.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.Contains("address/1", results);
            Assert.Contains("address/2", results);
            Assert.Contains("address/3", results);
            Assert.Contains("payload/1", results);
            Assert.Contains("payload/2", results);
            Assert.Contains("payload/3", results);
        }

        [Fact]
        public async Task RunWithoutConsumingMessage()
        {
            // Arrange
            var receivedCount = 0;
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var feature = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(TestStringPayload.ToString(feature));

                        // without consuming the message it will be processing again
                        if (++receivedCount == 3)
                        {
                            // to consume means deleting from mailbox
                            await feature.ConsumeAsync();
                        }
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), TestStringPayload.Create("payload"));

            var results = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                results.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.All(results, result => Assert.Equal("payload", result));
        }

        [Fact]
        public async Task RunWithDelayingMessage()
        {
            // Arrange
            var isReceivedAtFirstTime = true;
            var resultsChannel = Channel.CreateUnbounded<DateTime>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var feature = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(DateTime.UtcNow);

                        if (isReceivedAtFirstTime)
                        {
                            // the message remains in mailbox and will be ready to process after specified time
                            await feature.DelayAsync(TimeSpan.FromSeconds(1));

                            isReceivedAtFirstTime = false;
                        }
                        else
                        {
                            await feature.ConsumeAsync();
                        }
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

            var timeOfFirstReceiving = await resultsChannel.Reader.ReadAsync();
            var timeOfSecondReceiving = await resultsChannel.Reader.ReadAsync();

            await host.StopAsync();

            // Assert
            Assert.True(timeOfSecondReceiving - timeOfFirstReceiving >= TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task RunWithExpiringMessage()
        {
            // Arrange
            var isReceivedAtFirstTime = true;
            var resultsChannel = Channel.CreateUnbounded<string>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(async (context, token) =>
                    {
                        var feature = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        if (isReceivedAtFirstTime)
                        {
                            // the message will be deleted automatically after specified time
                            await feature.ExpireAsync(TimeSpan.FromMilliseconds(500));

                            // delaying and expiring are working independently
                            await feature.DelayAsync(TimeSpan.FromSeconds(1));

                            isReceivedAtFirstTime = false;
                        }
                        else
                        {
                            await resultsChannel.Writer.WriteAsync(TestStringPayload.ToString(feature));
                        }
                    }, actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), TestStringPayload.Create("payload"));

            await Task.Delay(TimeSpan.FromSeconds(2));

            await host.StopAsync();

            // Assert
            Assert.False(resultsChannel.Reader.TryRead(out _));
        }
    }
}
