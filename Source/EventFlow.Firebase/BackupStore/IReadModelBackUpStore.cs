using EventFlow.ReadStores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlow.Firebase.BackupStore
{
    public interface IReadModelBackUpStore
    {
        Task DeleteAllAsync<TReadModel>(string readModelDescription, CancellationToken cancellationToken)
            where TReadModel : class, IReadModel, new();

        Task DeleteOneAsync<TReadModel>(string readModelDescription, string readModelId, CancellationToken cancellationToken)
            where TReadModel : class, IReadModel, new();

        Task<TReadModel> GetAsync<TReadModel>(string readModelDescription, string readModelId, CancellationToken cancellationToken)
            where TReadModel : class, IReadModel, new();

        Task UpdateAsync<TReadModel>(string readModelDescription, string readModelId, TReadModel readModel, CancellationToken cancellationToken)
            where TReadModel : class, IReadModel, new();

        Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn>(Func<string, Task<TReturn>> firebaseMethod, string readModelDescription);

        Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn>(Func<string, Task<TReturn>> firebaseMethod, string readModelDescription, string readModelId);

        Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn, TData>(Func<string, TData, Task<TReturn>> firebaseMethod, string readModelDescription, string readModelId, TData data);

        void FixOutstandingFailedUpdates();
    }
}
