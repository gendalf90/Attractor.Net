using System;

namespace Attractor.Implementation
{
    public sealed class PID : IEquatable<PID>
    {
        private readonly Guid guid;
        
        private PID(Guid guid)
        {
            this.guid = guid;
        }
        
        bool IEquatable<PID>.Equals(PID other)
        {
            return Equals(other);
        }

        public override bool Equals(object obj)
        {
            return obj is PID pid && pid.guid == guid;
        }

        public override int GetHashCode()
        {
            return guid.GetHashCode();
        }

        public override string ToString()
        {
            return guid.ToString();
        }

        public static PID Generate()
        {
            return new PID(Guid.NewGuid());
        }

        public static PID Empty()
        {
            return new PID(Guid.Empty);
        }
    }
}