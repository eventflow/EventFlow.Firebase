using EventFlow.Extensions;
using EventFlow.Firebase.ReadStores.Attributes;
using EventFlow.Firebase.ValueObjects;
using EventFlow.ReadStores;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace EventFlow.Firebase.ReadStores
{
    public class ReadModelDescriptionProvider : IReadModelDescriptionProvider
    {
        private static readonly ConcurrentDictionary<Type, ReadModelDescription> NodeNames
            = new ConcurrentDictionary<Type, ReadModelDescription>();

        public ReadModelDescription GetReadModelDescription<TReadModel>()
            where TReadModel : IReadModel
        {
            return NodeNames.GetOrAdd(
                typeof(TReadModel),
                t =>
                {
                    var nodeNameAttr = t.GetCustomAttribute<FirebaseNodeNameAttribute>();
                    var nodeName = nodeNameAttr == null
                        ? $"eventflow-{typeof(TReadModel).PrettyPrint().ToLowerInvariant()}"
                        : nodeNameAttr.NodeName;
                    return new ReadModelDescription(new RootNodeName(nodeName));
                });
        }
    }
}
