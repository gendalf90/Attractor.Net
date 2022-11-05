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
            var stream = await host
                .Services
                .GetService<ISystem>()
                .GetAsync(StringBuffer.Address("address"));

            var request = await stream.SendAsync(EmptyBuffer.Payload());

            await request.WaitAsync();

            stream.Cancel();

            await stream.WaitAsync();

            // Assert
            Assert.True(isStarted);
            Assert.True(isReceived);
        }
    }
}
