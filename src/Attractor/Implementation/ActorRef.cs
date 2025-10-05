using System;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal class ActorRef(Process process) : ISelf
{
    public Task Send(IMessage message)
    {
        var awaiter = new RequestAwaiter();

        process.Send(Context.From(builder =>
        {
            message.Configure(builder);
            builder.Set<ISelf>(this);
            builder.Set(awaiter);
            builder.Set<IRequestAwaiter>(awaiter);
            builder.Set(message);
        }));

        return awaiter.Completion;
    }

    public void OnComplete(Action action)
    {
        process.OnComplete(action);
    }

    public void OnCancel(Action action)
    {
        process.OnCancel(action);
    }

    public void Dispose()
    {
        process.Dispose();
    }
}