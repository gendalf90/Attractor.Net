namespace Attractor.Implementation;

internal sealed class ActorRef(Process process) : ISelf
{
    public void Send(IMessage message)
    {
        process.Send(Context.From(builder =>
        {
            message.Configure(builder);
            builder.Set<ISelf>(this);
            builder.Set(message);
        }));
    }
}