using System;

namespace Attractor;

public interface IActorProcess : IActorRef, ICompletion, ICancellation, IDisposable;
