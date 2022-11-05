namespace Attractor.Implementation
{
    public static class EmptyBuffer
    {
        private static InternalEmptyBuffer instance = new InternalEmptyBuffer();

        public static IPayload Payload()
        {
            return instance;
        }

        private class InternalEmptyBuffer : IPayload
        {
            public void Accept(IVisitor visitor)
            {
            }

            public IPayload Clone()
            {
                return this;
            }
        }
    }
}
