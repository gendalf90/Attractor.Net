namespace Attractor.Implementation
{
    public static class Semaphore
    {
        public static ISemaphore Default(long limit)
        {
            return new SpinSemaphore(limit);
        }
    }
}