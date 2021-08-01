using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Attractor.Implementation.Message
{
    internal sealed class MemoryMailbox : IInbox, IAnonymousOutbox
    {
        private readonly Channel<ProcessingMessage> messageEventsChannel = Channel.CreateUnbounded<ProcessingMessage>();

        private readonly IOptions<MemoryMailboxSettings> options;

        public MemoryMailbox(IOptions<MemoryMailboxSettings> options)
        {
            this.options = options;
        }

        public async IAsyncEnumerable<IProcessingMessage> ReadMessagesAsync([EnumeratorCancellation] CancellationToken token = default)
        {
            while (true)
            {
                var trottleTask = options.Value.ReadTrottleTime.HasValue
                    ? Task.Delay(options.Value.ReadTrottleTime.Value, token)
                    : Task.CompletedTask;

                var message = await GetNextMessageAsync(token);
                
                await trottleTask;

                yield return message;
            }
        }

        private async ValueTask<ProcessingMessage> GetNextMessageAsync(CancellationToken token)
        {
            ProcessingMessage message;

            do
            {
                message = await messageEventsChannel.Reader.ReadAsync(token);
            }
            while (!message.TryStartProcessing());

            message.Initialize();

            return message;
        }

        public ValueTask SendMessageAsync(IAddress address, IPayload payload, SendingMetadata metadata = null, CancellationToken token = default)
        {
            return SendMessageAsync(address, payload, null, metadata, token);
        }

        private ValueTask SendMessageAsync(IAddress to, IPayload payload, IAddress from = null, SendingMetadata metadata = null, CancellationToken token = default)
        {
            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            token.ThrowIfCancellationRequested();

            var result = new ProcessingMessage(
                messageEventsChannel,
                message =>
                {
                    message.SetFeature<IReceivedMessageFeature>(new ReceivedMessageFeature(to, payload, message));
                    message.SetFeature<ISelfFeature>(new SelfFeature(to, this));

                    if (from != null)
                    {
                        message.SetFeature<ISenderFeature>(new SenderFeature(from, to, this));
                    }
                },
                to);

            InitializeMessage(result, metadata);

            return ValueTask.CompletedTask;
        }

        private void InitializeMessage(ProcessingMessage message, SendingMetadata metadata)
        {
            if (metadata?.Ttl != null)
            {
                message.Expire(metadata.Ttl.Value);
            }

            if (metadata?.Delay != null)
            {
                message.Delay(metadata.Delay.Value);
            }
            else
            {
                message.Add();
            }
        }

        private class ReceivedMessageFeature : IReceivedMessageFeature
        {
            private readonly ProcessingMessage message;
            private readonly IAddress current;
            private readonly IPayload payload;

            public ReceivedMessageFeature(IAddress current, IPayload payload, ProcessingMessage message)
            {
                this.current = current;
                this.payload = payload;
                this.message = message;
            }

            public ValueTask ConsumeAsync(CancellationToken token = default)
            {
                message.Consume();

                return ValueTask.CompletedTask;
            }

            public ValueTask DelayAsync(TimeSpan time, CancellationToken token = default)
            {
                message.Delay(time);

                return ValueTask.CompletedTask;
            }

            public ValueTask ExpireAsync(TimeSpan time, CancellationToken token = default)
            {
                message.Expire(time);

                return ValueTask.CompletedTask;
            }

            ReadOnlyMemory<byte> IAddress.GetBytes()
            {
                return current.GetBytes();
            }

            ReadOnlyMemory<byte> IPayload.GetBytes()
            {
                return payload.GetBytes();
            }
        }

        private class SelfFeature : ISelfFeature
        {
            private readonly MemoryMailbox mailbox;
            private readonly IAddress current;

            public SelfFeature(IAddress current, MemoryMailbox mailbox)
            {
                this.current = current;
                this.mailbox = mailbox;
            }

            public ReadOnlyMemory<byte> GetBytes()
            {
                return current.GetBytes();
            }

            public ValueTask SendMessageAsync(IAddress address, IPayload payload, SendingMetadata metadata = null, CancellationToken token = default)
            {
                return mailbox.SendMessageAsync(address, payload, current, metadata, token);
            }

            public ValueTask SendMessageAsync(IPayload payload, SendingMetadata metadata = null, CancellationToken token = default)
            {
                return SendMessageAsync(current, payload, metadata, token);
            }
        }

        private class SenderFeature : ISenderFeature
        {
            private readonly MemoryMailbox mailbox;
            private readonly IAddress current;
            private readonly IAddress sender;

            public SenderFeature(IAddress sender, IAddress current, MemoryMailbox mailbox)
            {
                this.current = current;
                this.sender = sender;
                this.mailbox = mailbox;
            }

            public ReadOnlyMemory<byte> GetBytes()
            {
                return sender.GetBytes();
            }

            public ValueTask SendMessageAsync(IPayload payload, SendingMetadata metadata = null, CancellationToken token = default)
            {
                return mailbox.SendMessageAsync(sender, payload, current, metadata, token);
            }
        }

        private class ProcessingMessage : IProcessingMessage
        {
            private readonly object sync = new object();
            private readonly ConcurrentDictionary<Type, object> features = new ConcurrentDictionary<Type, object>();

            private readonly Channel<ProcessingMessage> messageEventsChannel;
            private readonly Action<ProcessingMessage> featuresInitialization;
            private readonly IAddress current;

            private Timer delayingTimer;
            private Timer expiringTimer;
            private Guid delayingToken;
            private Guid expiringToken;
            private bool isProcessing;
            private bool isRemoved;
            private bool isDelayed;

            public ProcessingMessage(
                Channel<ProcessingMessage> messageEventsChannel,
                Action<ProcessingMessage> featuresInitialization, 
                IAddress current)
            {
                this.messageEventsChannel = messageEventsChannel;
                this.featuresInitialization = featuresInitialization;
                this.current = current;
            }

            public void Initialize()
            {
                features.Clear();
                featuresInitialization(this);
            }

            public void Add()
            {
                messageEventsChannel.Writer.TryWrite(this);
            }

            public bool TryStartProcessing()
            {
                lock (sync)
                {
                    if (isProcessing || isRemoved || isDelayed)
                    {
                        return false;
                    }

                    isProcessing = true;

                    return true;
                }
            }

            private void StopProcessing()
            {
                lock (sync)
                {
                    if (!isProcessing)
                    {
                        return;
                    }

                    isProcessing = false;

                    messageEventsChannel.Writer.TryWrite(this);
                }
            }

            public void Delay(TimeSpan time)
            {
                lock (sync)
                {
                    if (isRemoved)
                    {
                        return;
                    }

                    delayingTimer?.Dispose();

                    delayingToken = Guid.NewGuid();
                    isDelayed = true;
                    delayingTimer = new Timer(StopDelaying, delayingToken, time, Timeout.InfiniteTimeSpan);
                }
            }

            private void StopDelaying(object token)
            {
                lock (sync)
                {
                    if (!delayingToken.Equals(token))
                    {
                        return;
                    }

                    isDelayed = false;

                    delayingTimer?.Dispose();

                    messageEventsChannel.Writer.TryWrite(this);
                }
            }

            public void Expire(TimeSpan time)
            {
                lock (sync)
                {
                    if (isRemoved)
                    {
                        return;
                    }

                    expiringTimer?.Dispose();

                    expiringToken = Guid.NewGuid();
                    expiringTimer = new Timer(Expire, expiringToken, time, Timeout.InfiniteTimeSpan);
                }
            }

            private void Expire(object token)
            {
                lock (sync)
                {
                    if (expiringToken.Equals(token))
                    {
                        Remove();
                    }
                }
            }

            public void Consume()
            {
                lock (sync)
                {
                    Remove();
                }
            }

            private void Remove()
            {
                isRemoved = true;
                isDelayed = false;
                isProcessing = false;

                expiringTimer?.Dispose();
                delayingTimer?.Dispose();
            }

            public ValueTask DisposeAsync()
            {
                StopProcessing();

                return ValueTask.CompletedTask;
            }

            ReadOnlyMemory<byte> IAddress.GetBytes()
            {
                return current.GetBytes();
            }

            public T GetFeature<T>() where T : class
            {
                if (features.TryGetValue(typeof(T), out var result))
                {
                    return (T)result;
                }

                return null;
            }

            public void SetFeature<T>(T feature) where T : class
            {
                features.AddOrUpdate(typeof(T), feature, (_, _) => feature);
            }
        }
    }
}
