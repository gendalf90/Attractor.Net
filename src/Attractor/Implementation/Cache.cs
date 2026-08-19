namespace Attractor.Implementation;

public static class Cache
{
    public static ICache Round(IStage stage, int capacity)
    {
        ArgumentNullException.ThrowIfNull(stage, nameof(stage));

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        
        return new RoundCache(stage, capacity);
    }

    private class RoundCache : ICache
    {
        private readonly Strand strand = new();
        private readonly LinkedList<IAddress> queue = new();
        private readonly Dictionary<IAddress, IProxy> actors = new(Address.EqualityComparer);
        private readonly Latch latch;
        private readonly IStage stage;
        private readonly int capacity;

        public RoundCache(IStage stage, int capacity)
        {
            this.stage = stage;
            this.capacity = capacity;
            
            latch = new Latch(strand);
        }
        
        public IRef Get(IAddress address)
        {
            ArgumentNullException.ThrowIfNull(address, nameof(address));
            
            return new Reference(address, this);
        }

        private Task Send(IAddress address, IMessage message, CancellationToken token)
        {
            return strand.Run(async () =>
            {
                if (!actors.TryGetValue(address, out var actor))
                {
                    actor = await AddOrReplace(address, token);
                }

                await actor.Send(message, token);
            });
        }

        private async Task<IProxy> AddOrReplace(IAddress address, CancellationToken token)
        {
            using (await latch.Use(token))
            {
                await RemoveIfFull();

                var actor = stage.Play(address);

                queue.AddLast(address);
                actors.Add(address, actor);

                return actor;
            }
        }

        private async Task RemoveIfFull()
        {
            if (queue.Count < capacity)
            {
                return;
            }

            await using (actors[queue.First.Value])
            {
                actors.Remove(queue.First.Value);
                queue.RemoveFirst();
            }
        }

        private class Reference(IAddress address, RoundCache cache) : IRef
        {
            public Task Send(IMessage message, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(message, nameof(message));
                
                return cache.Send(address, message, token);
            }
        }
    }
}
