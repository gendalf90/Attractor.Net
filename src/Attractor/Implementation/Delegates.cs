using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

public delegate Task DecorateReceiveAsync(ReceiveAsync next, IContext context, CancellationToken token = default);

public delegate Task ReceiveAsync(IContext context, CancellationToken token = default);

public delegate void Receive(IContext context);

public delegate Task ReceiveAsync<T>(T value, IContext context, CancellationToken token = default);

public delegate void Receive<T>(T value, IContext context);

public delegate ValueTask DecorateDisposeAsync(Func<ValueTask> next);

public delegate ValueTask<bool> OnMatch(IContext context, CancellationToken token = default);

public delegate void Configure(IActorBuilder builder);

public delegate void DecorateConfigure(Configure next, IActorBuilder builder);
