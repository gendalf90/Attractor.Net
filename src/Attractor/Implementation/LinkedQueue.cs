using System.Threading;

namespace Attractor.Implementation
{
    internal sealed class LinkedQueue<T>
    {
        private readonly Node root;
        private volatile Node last;

        public LinkedQueue()
        {
            root = new Node();

            root.Next = root;

            last = root;
        }

        public void Enqueue(T value)
        {
            var node = new ValueNode
            {
                Value = value,
                Next = root
            };

            var current = last;

            do
            {
                current = Interlocked.CompareExchange(ref current.Next, node, root);
            }
            while (current != root);

            last = node;
        }

        public bool TryDequeue(out T value)
        {
            value = default;

            var first = root.Next;

            while (Interlocked.CompareExchange(ref root.Next, first.Next, first) != first)
            {
                first = root.Next;
            }

            Interlocked.CompareExchange(ref last, first.Next, first);

            if (first is not ValueNode valueNode)
            {
                return false;
            }

            value = valueNode.Value;
            
            return true;
        }

        private class Node
        {
            public volatile Node Next;
        }

        private class ValueNode : Node
        {
            public T Value;
        }
    }
}