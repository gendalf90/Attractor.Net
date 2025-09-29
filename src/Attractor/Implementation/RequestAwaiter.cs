using System.Threading.Tasks;

namespace Attractor.Implementation;

internal class RequestAwaiter : TaskCompletionSource, IRequestAwaiter
{
    public Task Completion => Task;
}