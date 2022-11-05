using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using System.Collections.Generic;
using Moq;
using System.Threading;
using System;
using Attractor.Implementation;

namespace Attractor.Tests
{
    public class AttractorTests
    {
        [Fact]
        public async Task StartAndReceiveSomeMessage()
        {
            // Arrange
            var isStarted = false;
            var isReceived = false;
            
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSystem();
                    services.MapStream(StringBuffer.Policy("address"), builder =>
                    {
                        builder.Chain(onStart: _ => isStarted = true, onReceive: _ => isReceived = true);
                    });
                })
                .Build();

            // Act
            await host
                .Services
                .GetService<ISystem>()
                .PostAsync(StringBuffer.Address("address"), EmptyBuffer.Payload());

            // Assert
            Assert.True(isStarted);
            Assert.True(isReceived);
        }

        [Fact]
        public async Task ReceiveStringMessage()
        {
            // Arrange
            var sentMessage = "message";
            var receivedMessage = string.Empty;

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSystem();
                    services.MapStream(StringBuffer.Policy("address"), builder =>
                    {
                        builder.Chain(StringBuffer.Process(msg => receivedMessage = msg));
                    });
                })
                .Build();

            // Act
            await host
                .Services
                .GetService<ISystem>()
                .PostAsync(StringBuffer.Address("address"), StringBuffer.Payload(sentMessage));

            // Assert
            Assert.Equal(sentMessage, receivedMessage);
        }

        [Fact]
        public async Task ChainMessages()
        {
            // Arrange
            var received = new List<int>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSystem();
                    services.MapStream(StringBuffer.Policy("address"), builder =>
                    {
                        builder.Chain(onReceive: _ => received.Add(1));
                        builder.Chain(onReceive: _ => received.Add(2));
                        builder.Chain(onReceive: _ => received.Add(3));
                    });
                })
                .Build();

            // Act
            await host
                .Services
                .GetService<ISystem>()
                .PostAsync(StringBuffer.Address("address"), EmptyBuffer.Payload());

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, received);
        }

        [Fact]
        public async Task DecorateMessages()
        {
            // Arrange
            var received = new List<int>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSystem();
                    services.MapStream(StringBuffer.Policy("address"), builder =>
                    {
                        builder.Decorate(onReceive: async (next, context) => { received.Add(1); await next(context); });
                        builder.Decorate(onReceive: async (next, context) => { received.Add(2); await next(context); });
                        builder.Decorate(onReceive: async (next, context) => { received.Add(3); await next(context); });
                    });
                })
                .Build();

            // Act
            await host
                .Services
                .GetService<ISystem>()
                .PostAsync(StringBuffer.Address("address"), EmptyBuffer.Payload());

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, received);
        }
    }
}
