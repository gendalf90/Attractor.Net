using System;
using System.Threading;

namespace Attractor
{
    public interface IActorProcess
    {
        void Clone();

        void Awake(TimeSpan delay = default, CancellationToken token = default);
    }
}