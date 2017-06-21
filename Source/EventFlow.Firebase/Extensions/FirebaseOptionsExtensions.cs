using EventFlow.Configuration;
using EventFlow.Extensions;
using EventFlow.Firebase.BackupStore;
using EventFlow.Firebase.Configuration;
using EventFlow.Firebase.ReadStores;
using EventFlow.ReadStores;
using FireSharp;
using FireSharp.Config;
using FireSharp.Interfaces;
using MongoDB.Driver;
using System;

namespace EventFlow.Firebase.Extensions
{
    public static class FirebaseOptionsExtensions
    {
        public static IEventFlowOptions ConfigureFirebase(
            this IEventFlowOptions eventFlowOptions,
            string firebasePath, 
            bool useBackupStore)
        {
            IFirebaseConfig config = new FirebaseConfig()
            {
                BasePath = firebasePath,
            };

            return eventFlowOptions
                .ConfigureFirebase(config, useBackupStore);
        }

        public static IEventFlowOptions ConfigureFirebase(
            this IEventFlowOptions eventFlowOptions,
            string firebasePath,
            string firebaseAuthSecret,
            bool useBackupStore)
        {
            IFirebaseConfig config = new FirebaseConfig()
            {
                BasePath = firebasePath,
                AuthSecret = firebaseAuthSecret,
            };

            return eventFlowOptions
                .ConfigureFirebase(config, useBackupStore);
        }

        public static IEventFlowOptions ConfigureFirebase(
            this IEventFlowOptions eventFlowOptions,
            string firebasePath,
            string firebaseAuthSecret,
            string mongoDbConnectionString,
            string mongoDbDatabase)
        {
            IFirebaseConfig config = new FirebaseConfig()
            {
                BasePath = firebasePath,
                AuthSecret = firebaseAuthSecret,
            };

            return eventFlowOptions
                .ConfigureMongoDb(mongoDbConnectionString, mongoDbDatabase)
                .ConfigureFirebase(config, true);
        }

        public static IEventFlowOptions ConfigureFirebase(
            this IEventFlowOptions eventFlowOptions,
            string firebasePath, 
            string mongoDbConnectionString,
            string mongoDbDatabase)
        {
            IFirebaseConfig config = new FirebaseConfig()
            {
                BasePath = firebasePath
            };

            return eventFlowOptions
                .ConfigureMongoDb(mongoDbConnectionString, mongoDbDatabase)
                .ConfigureFirebase(config, true);
        }

        private static IEventFlowOptions ConfigureFirebase(
            this IEventFlowOptions eventFlowOptions,
            IFirebaseConfig firebaseConfig, bool useBackupStore)
        {
            var firebaseClient = new FirebaseClient(firebaseConfig);
            return eventFlowOptions.ConfigureFirebase(() => firebaseClient, useBackupStore);
        }

        private static IEventFlowOptions ConfigureFirebase(
            this IEventFlowOptions eventFlowOptions,
            Func<IFirebaseClient> firebaseClientFactory, bool useBackupStore)
        {
            if (useBackupStore)
            {
                return eventFlowOptions.RegisterServices(sr =>
                {
                    sr.Register(f => firebaseClientFactory(), Lifetime.Singleton);
                    sr.Register<IReadModelDescriptionProvider, ReadModelDescriptionProvider>(Lifetime.Singleton, true);
                    sr.Register<IReadModelBackUpStore, ReadModelBackUpStore>();
                    sr.Register<IFirebaseReadStoreConfiguration>(f => new FirebaseReadStoreConfiguration(useBackupStore), Lifetime.Singleton);
                });
            }
            else
            {
                return eventFlowOptions.RegisterServices(sr =>
                {
                    sr.Register(f => firebaseClientFactory(), Lifetime.Singleton);
                    sr.Register<IReadModelDescriptionProvider, ReadModelDescriptionProvider>(Lifetime.Singleton, true);
                    sr.Register<IReadModelBackUpStore, MockReadModelBackUpStore>();
                    sr.Register<IFirebaseReadStoreConfiguration>(f => new FirebaseReadStoreConfiguration(useBackupStore), Lifetime.Singleton);
                });
            }
        }

        private static IEventFlowOptions ConfigureMongoDb(
            this IEventFlowOptions eventFlowOptions,
            string url,
            string database)
        {
            MongoUrl mongoUrl = new MongoUrl(url);
            var mongoClient = new MongoClient(mongoUrl);

            return eventFlowOptions
                .ConfigureMongoDb(mongoClient, database);
        }
        private static IEventFlowOptions ConfigureMongoDb(
            this IEventFlowOptions eventFlowOptions,
            IMongoClient mongoClient,
            string database)
        {
            IMongoDatabase mongoDatabase = mongoClient.GetDatabase(database);
            return eventFlowOptions.ConfigureMongoDb(() => mongoDatabase);
        }

        private static IEventFlowOptions ConfigureMongoDb(
            this IEventFlowOptions eventFlowOptions,
            Func<IMongoDatabase> mongoDatabaseFactory)
        {
            return eventFlowOptions.RegisterServices(sr =>
            {
                sr.Register(f => mongoDatabaseFactory(), Lifetime.Singleton);
            });
        }


        public static IEventFlowOptions UseFirebaseReadModel<TReadModel>(
            this IEventFlowOptions eventFlowOptions)
            where TReadModel : class, IFirebaseReadModel, new()
        {
            return eventFlowOptions
                .RegisterServices(f =>
                {
                    f.Register<IFirebaseReadModelStore<TReadModel>, FirebaseReadModelStore<TReadModel>>();
                    f.Register<IReadModelStore<TReadModel>>(r => r.Resolver.Resolve<IFirebaseReadModelStore<TReadModel>>());
                })
                .UseReadStoreFor<IFirebaseReadModelStore<TReadModel>, TReadModel>();
        }

        public static IEventFlowOptions UseFirebaseReadModel<TReadModel, TReadModelLocator>(
            this IEventFlowOptions eventFlowOptions)
            where TReadModel : class, IFirebaseReadModel, new()
            where TReadModelLocator : IReadModelLocator
        {
            return eventFlowOptions
                .RegisterServices(f =>
                {
                    f.Register<IFirebaseReadModelStore<TReadModel>, FirebaseReadModelStore<TReadModel>>();
                    f.Register<IReadModelStore<TReadModel>>(r => r.Resolver.Resolve<IFirebaseReadModelStore<TReadModel>>());
                })
                .UseReadStoreFor<IFirebaseReadModelStore<TReadModel>, TReadModel, TReadModelLocator>();
        }

        public static IEventFlowOptions UseFirebaseMappingReadModel<TReadModel>(
            this IEventFlowOptions eventFlowOptions)
            where TReadModel : class, IFirebaseMappingReadModel, new()
        {
            return eventFlowOptions
                .RegisterServices(f =>
                {
                    f.Register<IFirebaseMappingReadModelStore<TReadModel>, FirebaseMappingReadModelStore<TReadModel>>();
                    f.Register<IReadModelStore<TReadModel>>(r => r.Resolver.Resolve<IFirebaseMappingReadModelStore<TReadModel>>());
                })
                .UseReadStoreFor<IFirebaseMappingReadModelStore<TReadModel>, TReadModel>();
        }

        public static IEventFlowOptions UseFirebaseMappingReadModel<TReadModel, TReadModelLocator>(
            this IEventFlowOptions eventFlowOptions)
            where TReadModel : class, IFirebaseMappingReadModel, new()
            where TReadModelLocator : IReadModelLocator
        {
            return eventFlowOptions
                .RegisterServices(f =>
                {
                    f.Register<IFirebaseMappingReadModelStore<TReadModel>, FirebaseMappingReadModelStore<TReadModel>>();
                    f.Register<IReadModelStore<TReadModel>>(r => r.Resolver.Resolve<IFirebaseMappingReadModelStore<TReadModel>>());
                })
                .UseReadStoreFor<IFirebaseMappingReadModelStore<TReadModel>, TReadModel, TReadModelLocator>();
        }
    }
}
