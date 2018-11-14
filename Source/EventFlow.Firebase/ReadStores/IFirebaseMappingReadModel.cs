using EventFlow.ReadStores;
using System.Collections.Generic;

namespace EventFlow.Firebase.ReadStores
{
    public interface IFirebaseMappingReadModel : IReadModel
    {
        //List<string> Children { get; set; }
        Dictionary<string, object> Children { get; set; }
    }
}
