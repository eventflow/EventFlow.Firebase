using EventFlow.ValueObjects;
using System;
using System.Collections.Generic;

namespace EventFlow.Firebase.ValueObjects
{
    public class ReadModelDescription : ValueObject
    {
        public ReadModelDescription(RootNodeName rootNodeName)
        {
            RootNodeName = rootNodeName ?? throw new ArgumentNullException(nameof(rootNodeName));
        }

        public RootNodeName RootNodeName { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return RootNodeName;
        }
    }
}
