using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlow.Firebase.BackupStore
{
    public class MockReadModelBackUpStore : IReadModelBackUpStore
    {
        public void FixOutstandingFailedUpdates()
        {
            throw new NotImplementedException();
        }

        public Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn>(Func<string, Task<TReturn>> firebaseMethod, string readModelDescription)
        {
            throw new NotImplementedException();
        }

        public Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn>(Func<string, Task<TReturn>> firebaseMethod, string readModelDescription, string readModelId)
        {
            throw new NotImplementedException();
        }

        public Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn, TData>(Func<string, TData, Task<TReturn>> firebaseMethod, string readModelDescription, string readModelId, TData data)
        {
            throw new NotImplementedException();
        }

        Task IReadModelBackUpStore.DeleteAllAsync<TReadModel>(string readModelDescription, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task IReadModelBackUpStore.DeleteOneAsync<TReadModel>(string readModelDescription, string readModelId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<TReadModel> IReadModelBackUpStore.GetAsync<TReadModel>(string readModelDescription, string readModelId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task IReadModelBackUpStore.UpdateAsync<TReadModel>(string readModelDescription, string readModelId, TReadModel readModel, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
