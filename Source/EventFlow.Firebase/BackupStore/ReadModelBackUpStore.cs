using MongoDB.Driver;
using System;
using System.Threading.Tasks;
using EventFlow.ReadStores;
using System.Threading;
using EventFlow.Firebase.BackupStore.Model;
using MongoDB.Bson;
using EventFlow.Logs;
using System.Collections.Generic;
using System.Linq;
using FireSharp.Interfaces;
using MongoDB.Bson.Serialization;
using EventFlow.Firebase.ReadStores;
using EventFlow.Firebase.BackupStore.Constants;
using System.Reflection;

namespace EventFlow.Firebase.BackupStore
{

    public class ReadModelBackUpStore : IReadModelBackUpStore
    {
        const string BACKUP_COLLECTION_PREFIX = "firebase";
        const string FAILED_UPDATES_COLLECTION = "failedUpdates";
        const int ATTEMPTS = 3;

        private readonly IMongoDatabase _mongoDatabase;
        private readonly IFirebaseClient _firebaseClient;
        private readonly IReadModelDescriptionProvider _mappingReadModelDescriptionProvider;
        private readonly IReadModelDescriptionProvider _readModelDescriptionProvider;
        private readonly ILog _logger;

        public ReadModelBackUpStore(
            IMongoDatabase mongoDatabase,
            IFirebaseClient firebaseClient,
            IReadModelDescriptionProvider mappingReadModelDescriptionProvider,
            IReadModelDescriptionProvider readModelDescriptionProvider,
            ILog logger)
        {
            _mongoDatabase = mongoDatabase;
            _firebaseClient = firebaseClient;
            _mappingReadModelDescriptionProvider = mappingReadModelDescriptionProvider;
            _readModelDescriptionProvider = readModelDescriptionProvider;
            _logger = logger;
        }

        List<ReadModelUpdateFailure> GetFailedUpdatesToFix()
        {
            var collection = _mongoDatabase.GetCollection<ReadModelUpdateFailure>($"{BACKUP_COLLECTION_PREFIX}.{FAILED_UPDATES_COLLECTION}");

            var filterBuilder = Builders<ReadModelUpdateFailure>.Filter;
            var filter = filterBuilder.Eq(f => f.FixedAt, null);

            var items = collection
                .Find(filter)
                .ToList();

            return items;
        }

        TReadModel GetLatestBackupForReadModel<TReadModel>(string readModelDescription, string readModelId)
            where TReadModel : class, IReadModel, new()
        {
            var collection = _mongoDatabase.GetCollection<BsonDocument>($"{BACKUP_COLLECTION_PREFIX}.{readModelDescription}");

            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter =
                filterBuilder.Eq(ReadModelBackupProperties.READ_MODEL_DESCRIPTION, readModelDescription) &
                filterBuilder.Eq(ReadModelBackupProperties.READ_MODEL_ID, readModelId);

            var readModelBson = collection
                .Find(filter)
                .FirstOrDefault();

            return BsonSerializer.Deserialize<TReadModel>(readModelBson[ReadModelBackupProperties.READ_MODEL].ToJson());
        }

        void HandleFirebaseError<TReadModel>(Exception exception, string method, string readModelDescription)
        {
            LogFailedUpdate<TReadModel>(exception, method, readModelDescription, null);
        }

        void HandleFirebaseError<TReadModel>(Exception exception, string method, string readModelDescription, string readModelId)
        {
            LogFailedUpdate<TReadModel>(exception, method, readModelDescription, readModelId);
        }

        void LogFailedUpdate<TReadModel>(Exception exception, string method, string readModelDescription, string readModelId)
        {
            string exceptionMessage = $"LogFailedUpdate: {exception?.Message} {exception?.InnerException?.Message} {exception?.InnerException?.InnerException?.Message}";

            if (string.IsNullOrEmpty(readModelId))
                _logger.Error(exception, $"Firebase failed to excecute {method} for {readModelDescription} of type {typeof(TReadModel)}. {exceptionMessage}");
            else
                _logger.Error(exception, $"Firebase failed to excecute {method} for {readModelDescription} with id {readModelId} of type {typeof(TReadModel)}. {exceptionMessage}");

            var collection = _mongoDatabase.GetCollection<ReadModelUpdateFailure>($"{BACKUP_COLLECTION_PREFIX}.{FAILED_UPDATES_COLLECTION}");
            collection.InsertOne(
                new ReadModelUpdateFailure()
                {
                    ExceptionMessage = exceptionMessage,
                    FailedAt = DateTime.Now,
                    Attemps = 0,
                    FirebaseMethod = method,
                    ReadModelDescription = readModelDescription,
                    ReadModelId = readModelId,
                    ReadModelType = $"{typeof(TReadModel)},{typeof(TReadModel).Assembly.FullName}",
                    _id = ObjectId.GenerateNewId()
                });
        }

        void Restore(string readModelType, string readModelId, List<ReadModelUpdateFailure> failedUpdates)
        {

            var readModel = Activator.CreateInstance(Type.GetType(readModelType), true);
            var firebaseReadModelInterface = Type.GetType(readModelType).GetInterface("IFirebaseReadModel");
            var firebaseMappingReadModelInterface = Type.GetType(readModelType).GetInterface("IFirebaseMappingReadModel");

            if (firebaseReadModelInterface != null)
            {
                MethodInfo method = typeof(ReadModelBackUpStore).GetMethod("RestoreReadModel");
                MethodInfo generic = method.MakeGenericMethod(readModel.GetType());
                generic.Invoke(this, new object[] { readModelId, failedUpdates });
            }
            if (firebaseMappingReadModelInterface != null)
            {
                MethodInfo method = typeof(ReadModelBackUpStore).GetMethod("RestoreMappingReadModel");
                MethodInfo generic = method.MakeGenericMethod(readModel.GetType());
                generic.Invoke(this, new object[] { readModelId, failedUpdates });
            }
        }

        void MarkFailedUpdatesAsFixed(List<ReadModelUpdateFailure> failedUpdates)
        {
            failedUpdates.ForEach(async update =>
            {
                update.FixedAt = DateTime.Now;

                var collection = _mongoDatabase.GetCollection<ReadModelUpdateFailure>($"{BACKUP_COLLECTION_PREFIX}.{FAILED_UPDATES_COLLECTION}");
                var builder = Builders<ReadModelUpdateFailure>.Filter;
                var filter =
                    builder.Eq(p => p._id, update._id);

                await collection.ReplaceOneAsync(
                    filter,
                    update,
                    new UpdateOptions { IsUpsert = false });
            });
        }

        void MarkFailedUpdatesAsTriedAgain(List<ReadModelUpdateFailure> failedUpdates)
        {
            failedUpdates.ForEach(async update =>
            {
                update.LatestAttempt = DateTime.Now;
                update.Attemps = update.Attemps + 1;

                var collection = _mongoDatabase.GetCollection<ReadModelUpdateFailure>($"{BACKUP_COLLECTION_PREFIX}.{FAILED_UPDATES_COLLECTION}");
                var builder = Builders<ReadModelUpdateFailure>.Filter;
                var filter =
                    builder.Eq(p => p._id, update._id);

                await collection.ReplaceOneAsync(
                    filter,
                    update,
                    new UpdateOptions { IsUpsert = false });
            });
        }

        public void RestoreReadModel<TReadModel>(string readModelId, List<ReadModelUpdateFailure> failedUpdates)
            where TReadModel : class, IFirebaseReadModel, new()
        {
            var readModelDescription = _readModelDescriptionProvider.GetReadModelDescription<TReadModel>().RootNodeName.Value;
            var readmodelBackup = GetLatestBackupForReadModel<TReadModel>(readModelDescription, readModelId);
            var path = $"{readModelDescription}/{readModelId}";

            try
            {
                _firebaseClient.Set(path, readmodelBackup);
                MarkFailedUpdatesAsFixed(failedUpdates);
            }
            catch (Exception)
            {
                MarkFailedUpdatesAsTriedAgain(failedUpdates);
            }
        }

        public void RestoreMappingReadModel<TReadModel>(string readModelId, List<ReadModelUpdateFailure> failedUpdates)
            where TReadModel : class, IFirebaseMappingReadModel, new()
        {
            var readModelDescription = _readModelDescriptionProvider.GetReadModelDescription<TReadModel>().RootNodeName.Value;
            var readmodelBackup = GetLatestBackupForReadModel<TReadModel>(readModelDescription, readModelId);
            var path = $"{readModelDescription}/{readModelId}";

            try
            {
                if (readmodelBackup == null || readmodelBackup.Children == null || readmodelBackup.Children.Count == 0)
                    _firebaseClient.Delete(path);
                else
                    _firebaseClient.Set(path, readmodelBackup.Children);

                MarkFailedUpdatesAsFixed(failedUpdates);
            }
            catch (Exception)
            {
                MarkFailedUpdatesAsTriedAgain(failedUpdates);
            }
        }

        public Task DeleteAllAsync<TReadModel>(string readModelDescription, CancellationToken cancellationToken)
            where TReadModel : class, IReadModel, new()
        {
            var collection = _mongoDatabase.GetCollection<ReadModelBackup<TReadModel>>($"{BACKUP_COLLECTION_PREFIX}.{readModelDescription}");
            var filter = Builders<ReadModelBackup<TReadModel>>.Filter.Eq(model => model.ReadModelDescription, readModelDescription);
            return collection.DeleteManyAsync(filter, cancellationToken);
        }

        public Task DeleteOneAsync<TReadModel>(string readModelDescription, string readModelId, CancellationToken cancellationToken)
            where TReadModel : class, IReadModel, new()
        {
            var collection = _mongoDatabase.GetCollection<ReadModelBackup<TReadModel>>($"{BACKUP_COLLECTION_PREFIX}.{readModelDescription}");
            var builder = Builders<ReadModelBackup<TReadModel>>.Filter;
            var filter =
                builder.Eq(model => model.ReadModelDescription, readModelDescription) &
                builder.Eq(model => model.ReadModelId, readModelId);
            return collection.DeleteOneAsync(filter, cancellationToken);
        }

        public Task<TReadModel> GetAsync<TReadModel>(string readModelDescription, string readModelId, CancellationToken cancellationToken)
            where TReadModel : class, IReadModel, new()
        {
            var collection = _mongoDatabase.GetCollection<ReadModelBackup<TReadModel>>($"{BACKUP_COLLECTION_PREFIX}.{readModelDescription}");
            var builder = Builders<ReadModelBackup<TReadModel>>.Filter;
            var filter =
                builder.Eq(model => model.ReadModelDescription, readModelDescription) &
                builder.Eq(model => model.ReadModelId, readModelId);
            var backup = collection.Find(filter).FirstOrDefault();
            return Task.FromResult(backup == null ? default(TReadModel) : backup.ReadModel);
        }

        public async Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn>(Func<string, Task<TReturn>> firebaseMethod, string readModelDescription)
        {
            int attempt = 0;
            bool success = false;
            TReturn response = default(TReturn);
            Exception exception = null;
            while (!success && attempt < ATTEMPTS)
            {
                try
                {
                    response = await firebaseMethod(readModelDescription);
                    success = true;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    attempt++;
                }
            }

            if (!success)
            {
                HandleFirebaseError<TReadModel>(exception, firebaseMethod.Method.Name, readModelDescription);
            }

            return response;
        }

        public async Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn>(Func<string, Task<TReturn>> firebaseMethod, string readModelDescription, string readModelId)
        {
            var path = $"{readModelDescription}/{readModelId}";
            int attempt = 0;
            bool success = false;
            TReturn response = default(TReturn);
            Exception exception = null;
            while (!success && attempt < ATTEMPTS)
            {
                try
                {
                    response = await firebaseMethod(path);
                    success = true;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    attempt++;
                }
            }

            if (!success)
            {
                HandleFirebaseError<TReadModel>(exception, firebaseMethod.Method.Name, readModelDescription);
            }

            return response;
        }

        public async Task<TReturn> TryFirebaseCoupleOfTimesAsync<TReadModel, TReturn, TData>(Func<string, TData, Task<TReturn>> firebaseMethod, string readModelDescription, string readModelId, TData data)
        {
            var path = $"{readModelDescription}/{readModelId}";
            int attempt = 0;
            bool success = false;
            TReturn response = default(TReturn);
            Exception exception = null;
            while (!success && attempt < ATTEMPTS)
            {
                try
                {
                    response = await firebaseMethod(path, data);
                    success = true;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    attempt++;
                }
            }

            if (!success)
            {
                HandleFirebaseError<TReadModel>(exception, firebaseMethod.Method.Name, readModelDescription, readModelId);
            }

            return response;
        }

        public Task UpdateAsync<TReadModel>(string readModelDescription, string readModelId, TReadModel readModel, CancellationToken cancellationToken)
            where TReadModel : class, IReadModel, new()
        {
            var collection = _mongoDatabase.GetCollection<ReadModelBackup<TReadModel>>($"{BACKUP_COLLECTION_PREFIX}.{readModelDescription}");
            var builder = Builders<ReadModelBackup<TReadModel>>.Filter;
            var filter =
                builder.Eq(model => model.ReadModelDescription, readModelDescription) &
                builder.Eq(model => model.ReadModelId, readModelId);

            return collection.ReplaceOneAsync(
                filter,
                new ReadModelBackup<TReadModel>()
                {
                    ReadModel = readModel,
                    ReadModelDescription = readModelDescription,
                    ReadModelId = readModelId,
                    _id = $"{readModelDescription}-{readModelId}"
                },
                new UpdateOptions { IsUpsert = true });
        }

        public void FixOutstandingFailedUpdates()
        {
            var failedUpdates = GetFailedUpdatesToFix();
            var readModels = failedUpdates.GroupBy(group =>
                new
                {
                    group.ReadModelDescription,
                    group.ReadModelId,
                    group.ReadModelType
                }
            ).ToList();


            readModels.ForEach(readModel =>
            {
                Restore(readModel.Key.ReadModelType, readModel.Key.ReadModelId, readModel.ToList());
            });
        }
    }
}
