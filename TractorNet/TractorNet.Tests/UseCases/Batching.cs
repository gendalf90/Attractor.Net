using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using System.Collections.Generic;
using Moq;
using System.Threading;
using System;

namespace TractorNet.Tests.UseCases
{
    // by default new actor instance is created once a message is received but
    // it is possible to run an actor instance for processing a batch of messages
    public class Batching
    {
        private class BatchActor : IActor
        {
            private readonly Guid instanceId = Guid.NewGuid();
            private readonly Func<ReceivedMessageContext, Guid, CancellationToken, ValueTask> action;

            public BatchActor(Func<ReceivedMessageContext, Guid, CancellationToken, ValueTask> action)
            {
                this.action = action;
            }

            public ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                return action(context, instanceId, token);
            }
        }

        [Fact]
        public async Task RunWithExecutionTimeout()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<Guid>();
            var receiveCount = 0;

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(_ => new BatchActor(async (context, id, token) =>
                    {
                        // token is also in cancelled state after timeout is happened
                        token.ThrowIfCancellationRequested();

                        var feature = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(id);

                        if (++receiveCount == 4)
                        {
                            await feature.ConsumeAsync();
                        }
                        else
                        {
                            await feature.DelayAsync(TimeSpan.FromMilliseconds(700));
                        }
                    }), actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                        actorBuilder.UseBatching(config =>
                        {
                            // in other words it is the timeout of processing of batch for an actor instance
                            config.UseExecutionTimeout(TimeSpan.FromSeconds(1));
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            var instanceIds = new List<Guid>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());

            for (int i = 0; i < 4; i++)
            {
                instanceIds.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            // because a new actor instance will be created after timeout
            Assert.True(instanceIds[0] == instanceIds[1]);
            Assert.True(instanceIds[1] != instanceIds[2]);
            Assert.True(instanceIds[2] == instanceIds[3]);
        }

        [Fact]
        public async Task RunWithRunCountLimit()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<Guid>();
            var receiveCount = 0;

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(_ => new BatchActor(async (context, id, token) =>
                    {
                        var feature = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(id);

                        if (++receiveCount == 4)
                        {
                            await feature.ConsumeAsync();
                        }
                        else
                        {
                            await feature.DelayAsync(TimeSpan.FromMilliseconds(100));
                        }
                    }), actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                        actorBuilder.UseBatching(config =>
                        {
                            // after the limit is exceeded a new instace will be created
                            config.UseRunCountLimit(2);
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            var instanceIds = new List<Guid>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());

            for (int i = 0; i < 4; i++)
            {
                instanceIds.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.True(instanceIds[0] == instanceIds[1]);
            Assert.True(instanceIds[1] != instanceIds[2]);
            Assert.True(instanceIds[2] == instanceIds[3]);
        }

        [Fact]
        public async Task RunWithMessageReceivingTimeout()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<Guid>();
            var receiveCount = 0;

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(_ => new BatchActor(async (context, id, token) =>
                    {
                        var feature = context.Metadata.GetFeature<IReceivedMessageFeature>();

                        await resultsChannel.Writer.WriteAsync(id);

                        if (++receiveCount == 2)
                        {
                            await feature.ConsumeAsync();
                        }
                        else
                        {
                            await feature.DelayAsync(TimeSpan.FromSeconds(1));
                        }
                    }), actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                        actorBuilder.UseBatching(config =>
                        {
                            // if there is no new message during the timeout the instance is stopped
                            config.UseMessageReceivingTimeout(TimeSpan.FromMilliseconds(700));
                        });
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            var instanceIds = new List<Guid>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());

            for (int i = 0; i < 2; i++)
            {
                instanceIds.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.True(instanceIds[0] != instanceIds[1]);
        }

        [Fact]
        public async Task RunWithBatchFeature()
        {
            // Arrange
            var resultsChannel = Channel.CreateUnbounded<Guid>();
            var receiveCount = 0;

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddTractorServer();
                    services.RegisterActor(_ => new BatchActor(async (context, id, token) =>
                    {
                        var messageFeature = context.Metadata.GetFeature<IReceivedMessageFeature>();
                        var batchFeature = context.Metadata.GetFeature<IBatchFeature>();

                        await resultsChannel.Writer.WriteAsync(id);

                        receiveCount++;

                        if (receiveCount == 4)
                        {
                            await messageFeature.ConsumeAsync();
                        }
                        else if (receiveCount == 2)
                        {
                            // call this to break the current instance batch processing
                            batchFeature.StopProcessing();
                        }
                        else
                        {
                            await messageFeature.DelayAsync(TimeSpan.FromMilliseconds(100));
                        }
                    }), actorBuilder =>
                    {
                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                        actorBuilder.UseBatching();
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            var instanceIds = new List<Guid>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());

            for (int i = 0; i < 4; i++)
            {
                instanceIds.Add(await resultsChannel.Reader.ReadAsync());
            }

            await host.StopAsync();

            // Assert
            Assert.True(instanceIds[0] == instanceIds[1]);
            Assert.True(instanceIds[1] != instanceIds[2]);
            Assert.True(instanceIds[2] == instanceIds[3]);
        }
    }
}
