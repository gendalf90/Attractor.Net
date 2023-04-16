using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class LinkedListMailbox : IMailbox
    {
        private readonly Node root;
        private readonly Node last;

        public LinkedListMailbox()
        {
            root = Node.CreateRoot();
            last = new Node
            {
                Next = root
            };
        }

        public ValueTask SendAsync(IContext message, CancellationToken token = default)
        {
            var node = new ContextNode(message)
            {
                Next = root
            };

            node.UpdateLastInRangeOf(last, root);

            return ValueTask.CompletedTask;
        }

        public ValueTask<IContext> ReceiveAsync(CancellationToken token = default)
        {
            var first = root.Next;

            if (first is not ContextNode node)
            {
                return ValueTask.FromResult<IContext>(null);
            }

            node.Context.Set<IConsumable>(new Consumer(this, node));

            return ValueTask.FromResult(node.Context);
        }

        private class Consumer : IConsumable
        {
            private readonly LinkedListMailbox mailbox;
            private readonly Node node;

            public Consumer(LinkedListMailbox mailbox, Node node)
            {
                this.mailbox = mailbox;
                this.node = node;
            }
            
            public ValueTask ConsumeAsync(CancellationToken token = default)
            {
                node.RemoveIfNextOf(mailbox.root);
                node.RemoveIfNextOf(mailbox.last);

                return ValueTask.CompletedTask;
            }
        }

        private class Node
        {
            public volatile Node next;

            public Node Next { get => next; set => next = value; }

            public void UpdateLastInRangeOf(Node last, Node end)
            {
                var current = last.next;
                
                do
                {
                    current = Interlocked.CompareExchange(ref current.next, this, end);
                }
                while (current != end);

                last.next = this;
            }

            public void RemoveIfNextOf(Node node)
            {
                Interlocked.CompareExchange(ref node.next, next, this);
            }

            public static Node CreateRoot()
            {
                var result = new Node();

                result.next = result;

                return result;
            }
        }

        private class ContextNode : Node
        {
            public ContextNode(IContext context)
            {
                Context = context;
            }

            public IContext Context { get; }
        }
    }
}