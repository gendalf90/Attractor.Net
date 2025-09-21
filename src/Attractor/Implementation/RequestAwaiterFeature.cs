using System.Threading.Tasks;

namespace Attractor.Implementation;

internal class RequestAwaiterFeature : TaskCompletionSource, IRequestAwaiterFeature
{
    public Task Completion => Task;
}