using EventFlow.Aggregates;
using EventFlow.Extensions;
using EventFlow.Firebase.BackupStore;
using EventFlow.Firebase.Configuration;
using EventFlow.Logs;
using EventFlow.ReadStores;
using FireSharp.Interfaces;
using FireSharp.Response;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlow.Firebase.ReadStores
{
    public class FirebaseReadModelStore<TReadModel> : IFirebaseReadModelStore<TReadModel>
        where TReadModel : class, IFirebaseReadModel, new()
    {

        private readonly ILog _log;
        private readonly IFirebaseClient _firebaseClient;
        private readonly IReadModelDescriptionProvider _readModelDescriptionProvider;
        private readonly IReadModelBackUpStore _readModelBackUpStore;
        private readonly IFirebaseReadStoreConfiguration _firebaseReadStoreConfiguration;


        public FirebaseReadModelStore(
            ILog log,
            IFirebaseClient firebaseClient,
            IReadModelDescriptionProvider readModelDescriptionProvider,
            IReadModelBackUpStore readModelBackUpStore,
            IFirebaseReadStoreConfiguration firebaseReadStoreConfiguration
            )
        {
            _log = log;
            _firebaseClient = firebaseClient;
            _readModelDescriptionProvider = readModelDescriptionProvider;
            _readModelBackUpStore = readModelBackUpStore;
            _firebaseReadStoreConfiguration = firebaseReadStoreConfiguration;
        }

        public async Task DeleteAllAsync(
            CancellationToken cancellationToken)
        {
            var readModelDescription = _readModelDescriptionProvider.GetReadModelDescription<TReadModel>();

            _log.Information($"Deleting ALL '{typeof(TReadModel).PrettyPrint()}' by DELETING NODE '{readModelDescription.RootNodeName}'!");

            if (_firebaseReadStoreConfiguration.UseBackupStore)
            {
                await _readModelBackUpStore.DeleteAllAsync<TReadModel>(readModelDescription.RootNodeName.Value, cancellationToken);
                await _readModelBackUpStore.TryFirebaseCoupleOfTimesAsync<TReadModel, FirebaseResponse>(_firebaseClient.DeleteAsync, readModelDescription.RootNodeName.Value);
            }
            else
                await _firebaseClient.DeleteAsync(readModelDescription.RootNodeName.Value);
        }

        public async Task DeleteAsync(
            string id, 
            CancellationToken cancellationToken)
        {
            var readModelDescription = _readModelDescriptionProvider.GetReadModelDescription<TReadModel>();

            _log.Information($"Deleting ONE '{typeof(TReadModel).PrettyPrint()}' WITH PATH '{readModelDescription.RootNodeName}/{id}'!");
            if (_firebaseReadStoreConfiguration.UseBackupStore)
            {
                await _readModelBackUpStore.DeleteOneAsync<TReadModel>(readModelDescription.RootNodeName.Value, id, cancellationToken);
                await _readModelBackUpStore.TryFirebaseCoupleOfTimesAsync<TReadModel, FirebaseResponse>(_firebaseClient.DeleteAsync, readModelDescription.RootNodeName.Value, id);
            }
            else
            {
                await _firebaseClient.DeleteAsync($"{readModelDescription.RootNodeName}/{id}");
            }
        }

        public async Task<ReadModelEnvelope<TReadModel>> GetAsync(
            string id, 
            CancellationToken cancellationToken)
        {
            var readModelDescription = _readModelDescriptionProvider.GetReadModelDescription<TReadModel>();

            _log.Verbose(() => $"Fetching read model '{typeof(TReadModel).PrettyPrint()}' with ID '{id}' from node '{readModelDescription.RootNodeName}'");

            TReadModel readModel = null;
            if (_firebaseReadStoreConfiguration.UseBackupStore)
                readModel = await _readModelBackUpStore.GetAsync<TReadModel>(readModelDescription.RootNodeName.Value, id, cancellationToken);
            else
            {
                var response = await _firebaseClient.GetAsync($"{readModelDescription.RootNodeName}/{id}");
                readModel = response.ResultAs<TReadModel>();
            }

            return ReadModelEnvelope<TReadModel>.With(id, readModel);
        }

        public async Task UpdateAsync(
            IReadOnlyCollection<ReadModelUpdate> readModelUpdates, 
            IReadModelContextFactory readModelContextFactory, 
            Func<IReadModelContext, IReadOnlyCollection<IDomainEvent>, ReadModelEnvelope<TReadModel>, CancellationToken, Task<ReadModelUpdateResult<TReadModel>>> updateReadModel, 
            CancellationToken cancellationToken)
        {
            var readModelDescription = _readModelDescriptionProvider.GetReadModelDescription<TReadModel>();

            _log.Verbose(() =>
            {
                var readModelIds = readModelUpdates
                    .Select(u => u.ReadModelId)
                    .Distinct()
                    .OrderBy(i => i)
                    .ToList();
                return $"Updating read models of type '{typeof(TReadModel).PrettyPrint()}' with IDs '{string.Join(", ", readModelIds)}' in node '{readModelDescription.RootNodeName}'";
            });

            foreach (var readModelUpdate in readModelUpdates)
            {
                try
                {
                    TReadModel firebaseResult = null;

                    if (_firebaseReadStoreConfiguration.UseBackupStore)
                        firebaseResult = await _readModelBackUpStore.GetAsync<TReadModel>(readModelDescription.RootNodeName.Value, readModelUpdate.ReadModelId, cancellationToken);
                    else
                    {
                        var response = await _firebaseClient.GetAsync($"{readModelDescription.RootNodeName}/{readModelUpdate.ReadModelId}");
                        firebaseResult = response.ResultAs<TReadModel>();
                    }

                    var readModelEnvelope = (firebaseResult != null)
                        ? ReadModelEnvelope<TReadModel>.With(readModelUpdate.ReadModelId, firebaseResult)
                        : ReadModelEnvelope<TReadModel>.Empty(readModelUpdate.ReadModelId);

                    var readModelContext = readModelContextFactory.Create("", firebaseResult == null);

                    var readModelUpdateResult = await updateReadModel(
                        readModelContext,
                        readModelUpdate.DomainEvents,
                        readModelEnvelope,
                        cancellationToken).ConfigureAwait(false);

                    readModelEnvelope = readModelUpdateResult.Envelope;

                    readModelEnvelope.ReadModel._version = readModelEnvelope.Version;

                    if (_firebaseReadStoreConfiguration.UseBackupStore)
                    {
                        await _readModelBackUpStore.UpdateAsync(readModelDescription.RootNodeName.Value, readModelUpdate.ReadModelId, readModelEnvelope.ReadModel, cancellationToken);
                        await _readModelBackUpStore.TryFirebaseCoupleOfTimesAsync<TReadModel, SetResponse, TReadModel>(_firebaseClient.SetAsync, readModelDescription.RootNodeName.Value, readModelUpdate.ReadModelId, readModelEnvelope.ReadModel);
                    }
                    else
                        await _firebaseClient.SetAsync($"{readModelDescription.RootNodeName}/{readModelUpdate.ReadModelId}", readModelEnvelope.ReadModel);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        
    }
}
