using EventFlow.Aggregates;
using EventFlow.Extensions;
using EventFlow.Firebase.BackupStore;
using EventFlow.Firebase.Configuration;
using EventFlow.Logs;
using EventFlow.ReadStores;
using FireSharp.Interfaces;
using FireSharp.Response;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlow.Firebase.ReadStores
{
    public class FirebaseMappingReadModelStore<TReadModel> : IFirebaseMappingReadModelStore<TReadModel>
        where TReadModel : class, IFirebaseMappingReadModel, new()
    {
        private readonly ILog _log;
        private readonly IFirebaseClient _firebaseClient;
        private readonly IReadModelDescriptionProvider _readModelDescriptionProvider;
        private readonly IReadModelBackUpStore _readModelBackUpStore;
        private readonly IFirebaseReadStoreConfiguration _firebaseReadStoreConfiguration;

        public FirebaseMappingReadModelStore(
            ILog log,
            IFirebaseClient firebaseClient,
            IReadModelDescriptionProvider readModelDescriptionProvider,
            IReadModelBackUpStore readModelBackUpStore,
            IFirebaseReadStoreConfiguration firebaseReadStoreConfiguration)
        {
            _log = log;
            _firebaseClient = firebaseClient;
            _readModelDescriptionProvider = readModelDescriptionProvider;
            _readModelBackUpStore = readModelBackUpStore;
            _firebaseReadStoreConfiguration = firebaseReadStoreConfiguration;
        }
        public async Task DeleteAllAsync(CancellationToken cancellationToken)
        {
            var readModelDescription = _readModelDescriptionProvider.GetReadModelDescription<TReadModel>();

            _log.Information($"Deleting ALL '{typeof(TReadModel).PrettyPrint()}' by DELETING NODE '{readModelDescription.RootNodeName}'!");
            if (_firebaseReadStoreConfiguration.UseBackupStore)
            {
                await _readModelBackUpStore.DeleteAllAsync<TReadModel>(readModelDescription.RootNodeName.Value, cancellationToken);
                await _readModelBackUpStore.TryFirebaseCoupleOfTimesAsync<TReadModel, FirebaseResponse>(_firebaseClient.DeleteAsync, readModelDescription.RootNodeName.Value);
            }
            else
            {
                await _firebaseClient.DeleteAsync(readModelDescription.RootNodeName.Value);
            }
        }

        public async Task<ReadModelEnvelope<TReadModel>> GetAsync(string id, CancellationToken cancellationToken)
        {
            var readModelDescription = _readModelDescriptionProvider.GetReadModelDescription<TReadModel>();

            _log.Verbose(() => $"Fetching read model '{typeof(TReadModel).PrettyPrint()}' with ID '{id}' from node '{readModelDescription.RootNodeName}'");

            TReadModel readModel = null;
            if (_firebaseReadStoreConfiguration.UseBackupStore)
                readModel = await _readModelBackUpStore.GetAsync<TReadModel>(readModelDescription.RootNodeName.Value, id, cancellationToken);
            else
            {
                var response = await _firebaseClient.GetAsync($"{readModelDescription.RootNodeName}/{id}");
                var dynamicResult = response.ResultAs<dynamic>();
                Dictionary<string, object> children = new Dictionary<string, object>();

                foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(dynamicResult))
                {
                    children.Add(prop.Name, true);
                }

                readModel = new TReadModel();
                readModel.Children = children;
            }

            return ReadModelEnvelope<TReadModel>.With(id, readModel);
        }

        public async Task UpdateAsync(
            IReadOnlyCollection<ReadModelUpdate> readModelUpdates,
            IReadModelContext readModelContext,
            Func<IReadModelContext, IReadOnlyCollection<IDomainEvent>, ReadModelEnvelope<TReadModel>, CancellationToken, Task<ReadModelEnvelope<TReadModel>>> updateReadModel,
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

                        var dynamicResult = response.ResultAs<dynamic>();

                        if (dynamicResult != null)
                        {
                            Dictionary<string, object> children = new Dictionary<string, object>();

                            foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(dynamicResult))
                            {
                                children.Add(prop.Name, true);
                            }

                            firebaseResult = new TReadModel();
                            firebaseResult.Children = children;
                        }

                    }

                    var readModelEnvelope = (firebaseResult != null)
                        ? ReadModelEnvelope<TReadModel>.With(readModelUpdate.ReadModelId, firebaseResult)
                        : ReadModelEnvelope<TReadModel>.Empty(readModelUpdate.ReadModelId);

                    readModelEnvelope = await updateReadModel(
                        readModelContext,
                        readModelUpdate.DomainEvents,
                        readModelEnvelope,
                        cancellationToken).ConfigureAwait(false);

                    if (_firebaseReadStoreConfiguration.UseBackupStore)
                    {
                        if (readModelEnvelope.ReadModel.Children != null && readModelEnvelope.ReadModel.Children.Count > 0)
                        {
                            await _readModelBackUpStore.UpdateAsync(readModelDescription.RootNodeName.Value, readModelUpdate.ReadModelId, readModelEnvelope.ReadModel, cancellationToken);
                            await _readModelBackUpStore.TryFirebaseCoupleOfTimesAsync<TReadModel, SetResponse, Dictionary<string, object>>(_firebaseClient.SetAsync, readModelDescription.RootNodeName.Value, readModelUpdate.ReadModelId, readModelEnvelope.ReadModel.Children);
                        }
                        else
                        {
                            await _readModelBackUpStore.DeleteOneAsync<TReadModel>(readModelDescription.RootNodeName.Value, readModelUpdate.ReadModelId, cancellationToken);
                            await _readModelBackUpStore.TryFirebaseCoupleOfTimesAsync<TReadModel, FirebaseResponse>(_firebaseClient.DeleteAsync, readModelDescription.RootNodeName.Value, readModelUpdate.ReadModelId);
                        }
                    }
                    else
                    {
                        if (readModelEnvelope.ReadModel.Children != null && readModelEnvelope.ReadModel.Children.Count > 0)
                            await _firebaseClient.SetAsync($"{readModelDescription.RootNodeName}/{readModelUpdate.ReadModelId}", readModelEnvelope.ReadModel.Children);
                        else
                            await _firebaseClient.DeleteAsync($"{readModelDescription.RootNodeName}/{readModelUpdate.ReadModelId}");
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}
