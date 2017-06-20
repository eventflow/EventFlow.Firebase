using EventFlow.ReadStores;

namespace EventFlow.Firebase.ReadStores
{
    public interface IFirebaseMappingReadModelStore<TReadModel> : IReadModelStore<TReadModel>
        where TReadModel : class, IFirebaseMappingReadModel, new()
    {
    }
}
