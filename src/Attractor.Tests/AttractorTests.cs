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
using Newtonsoft.Json.Linq;
using System.Net;
using Xunit.Sdk;

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
            var system = host.Services.GetService<ISystem>();

            await using (var reference = await system.UseAsync(StringBuffer.Address("address")))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

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
            var system = host.Services.GetService<ISystem>();

            await using (var reference = await system.UseAsync(StringBuffer.Address("address")))
            {
                await reference.PostAsync(StringBuffer.Payload(sentMessage));
            }

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
            var system = host.Services.GetService<ISystem>();

            await using (var reference = await system.UseAsync(StringBuffer.Address("address")))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

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
            var system = host.Services.GetService<ISystem>();

            await using (var reference = await system.UseAsync(StringBuffer.Address("address")))
            {
                await reference.PostAsync(EmptyBuffer.Payload());
            }

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, received);
        }
    }
}
