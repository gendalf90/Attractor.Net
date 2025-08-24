using System;
using Microsoft.Extensions.Hosting;

namespace Attractor;

public interface ISystem : IHostedService
{
    void Register(IAddressPolicy policy, Action<IActorBuilder> configuration = null);
}