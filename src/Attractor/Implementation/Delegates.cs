using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

public delegate Task DecorateReceiveAsync(ReceiveAsync next, IContext context, CancellationToken token = default);

public delegate Task ReceiveAsync(IContext context, CancellationToken token = default);

public delegate void Receive(IContext context);

public delegate Task ReceiveAsync<T>(T value, CancellationToken token = default);

public delegate void Receive<T>(T value);

public delegate void Configure(IBuilder<IHandler> builder);

public delegate void DecorateConfigure(Configure next, IBuilder<IHandler> builder);
