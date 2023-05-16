namespace Attractor.Implementation
{
    public sealed class StoppingMessage : IPayload
    {
        public static StoppingMessage Instance { get; } = new StoppingMessage();

        void IVisitable.Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }
    }
}