﻿using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Threading.Channels;

namespace Attractor.Tests.UseCases
{
    public class Decorators
    {
        private class FirstCommonActorDecorator : IActorDecorator
        {
            private Channel<int> resultsChannel;
            private IActor nextActor;

            public FirstCommonActorDecorator(Channel<int> resultsChannel)
            {
                this.resultsChannel = resultsChannel;
            }

            public void Decorate(IActor actor)
            {
                nextActor = actor;
            }

            public async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                await resultsChannel.Writer.WriteAsync(1);
                await nextActor.OnReceiveAsync(context, token);
                await resultsChannel.Writer.WriteAsync(13);

                resultsChannel.Writer.Complete();
            }
        }

        private class FirstActorDecorator : IActorDecorator
        {
            private Channel<int> resultsChannel;
            private IActor nextActor;

            public FirstActorDecorator(Channel<int> resultsChannel)
            {
                this.resultsChannel = resultsChannel;
            }

            public void Decorate(IActor actor)
            {
                nextActor = actor;
            }

            public async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                await resultsChannel.Writer.WriteAsync(4);
                await nextActor.OnReceiveAsync(context, token);
                await resultsChannel.Writer.WriteAsync(10);
            }
        }

        private class ParameterizedActorDecorator : IActorDecorator
        {
            private Channel<int> resultsChannel;
            private IActor nextActor;
            private int beforeReceivingValue;
            private int afterReceivingValue;

            public ParameterizedActorDecorator(int beforeReceivingValue, int afterReceivingValue, Channel<int> resultsChannel)
            {
                this.beforeReceivingValue = beforeReceivingValue;
                this.afterReceivingValue = afterReceivingValue;
                this.resultsChannel = resultsChannel;
            }

            public void Decorate(IActor actor)
            {
                nextActor = actor;
            }

            public async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                await resultsChannel.Writer.WriteAsync(beforeReceivingValue);
                await nextActor.OnReceiveAsync(context, token);
                await resultsChannel.Writer.WriteAsync(afterReceivingValue);
            }
        }

        [Fact]
        public async Task Run()
        {
            // Arrange
            var completionTimeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var completionResultsChannel = Channel.CreateUnbounded<int>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(completionResultsChannel);
                    services.AddAttractorServer();

                    // common decorators that are applied to each actor
                    services.UseDecorator<FirstCommonActorDecorator>();
                    services.UseDecorator(provider =>
                    {
                        return new ParameterizedActorDecorator(2, 12, provider.GetRequiredService<Channel<int>>());
                    });
                    services.UseDecorator(async (actor, context, token) =>
                    {
                        await completionResultsChannel.Writer.WriteAsync(3);
                        await actor.OnReceiveAsync(context, token);
                        await completionResultsChannel.Writer.WriteAsync(11);
                    });

                    services.RegisterActor(async (context, token) =>
                    {
                        await completionResultsChannel.Writer.WriteAsync(7);
                    }, actorBuilder =>
                    {
                        // specific actor decorators
                        actorBuilder.UseDecorator<FirstActorDecorator>();
                        actorBuilder.UseDecorator(provider =>
                        {
                            return new ParameterizedActorDecorator(5, 9, provider.GetRequiredService<Channel<int>>());
                        });
                        actorBuilder.UseDecorator(async (actor, context, token) =>
                        {
                            await completionResultsChannel.Writer.WriteAsync(6);
                            await actor.OnReceiveAsync(context, token);
                            await completionResultsChannel.Writer.WriteAsync(8);
                        });
                        actorBuilder.UseAutoConsume();

                        actorBuilder.UseAddressPolicy(_ => TestStringAddress.CreatePolicy("address"));
                    });
                })
                .Build();

            // Act
            await host.StartAsync();

            var outbox = host.Services.GetRequiredService<IAnonymousOutbox>();

            await outbox.SendMessageAsync(TestStringAddress.CreateAddress("address"), Mock.Of<IPayload>());

            // Assert
            for (int expectedResult = 1; expectedResult <= 13; expectedResult++)
            {
                Assert.Equal(expectedResult, await completionResultsChannel.Reader.ReadAsync(completionTimeoutSource.Token));
            }

            await host.StopAsync();
        }
    }
}
