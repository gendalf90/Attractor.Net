using System;

namespace Attractor.Implementation;

internal class StartMessage(Action<Process> onComplete = null)
{
    public void Complete(Process process)
    {
        onComplete?.Invoke(process);
    }
}