using EventFlow.ReadStores;

namespace EventFlow.Firebase.ReadStores
{
    public interface IFirebaseReadModel : IReadModel
    {
        // TODO: Don't want _version for read models with locators
        long? _version { get; set; }
    }
}
