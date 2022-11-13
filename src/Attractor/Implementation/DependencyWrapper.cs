namespace Attractor.Implementation
{
    internal class DependencyWrapper<TValue>
    {
        private readonly TValue value;

        public DependencyWrapper(TValue value)
        {
            this.value = value;
        }

        public TValue Value { get => value; }
    }
}
