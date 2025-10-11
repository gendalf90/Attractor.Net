using System;

namespace Attractor;

public interface IActorProcess : IActorRef, IDisposable
{
    Guid PID { get; }
    
    void OnComplete(Action action);

    void OnCancel(Action action);
}
