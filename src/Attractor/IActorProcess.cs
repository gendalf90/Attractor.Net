using System;

namespace Attractor;

public interface IActorProcess : IActorRef, IDisposable
{
    void OnComplete(Action action);

    void OnCancel(Action action);
}
