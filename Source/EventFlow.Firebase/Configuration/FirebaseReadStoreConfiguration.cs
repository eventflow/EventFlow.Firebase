using System;

namespace EventFlow.Firebase.Configuration
{
    public class FirebaseReadStoreConfiguration : IFirebaseReadStoreConfiguration
    {
        bool _useBackupStore { get; }

        public bool UseBackupStore => _useBackupStore;


        public FirebaseReadStoreConfiguration(bool useBackupStore)
        {
            _useBackupStore = useBackupStore;
        }
    }
}
