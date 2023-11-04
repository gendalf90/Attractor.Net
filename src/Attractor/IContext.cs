using System;
using System.Collections.Generic;

namespace Attractor
{
    public interface IContext : IDictionary<object, object>
    {
        void ForEach(Action<KeyValuePair<object, object>> action);
    }
}