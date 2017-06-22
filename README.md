# EventFlow.Firebase
EventFlow.Firebase offers Firebase functionality to the [EventFlow](https://github.com/eventflow/EventFlow) package.

### Features
* FirebaseReadModel - a normal read model which is persisted in Firebase.
* FirebaseMappingReadModel - A read model to create mappings in Firebase.
* BackUpStore - Serves to rebuild the read models and/or recover read models when Firebase is down.

### Installation
Run the following command in the [Package Manager Console](https://docs.microsoft.com/en-us/nuget/tools/package-manager-console):
```
Install-Package EventFlow.Firebase
```

### Configuration
#### The need for a back-up store explained
The need came from the following scenario:
We are based in South Africa and are depended on trans ocean cabling to reach Google's datacenter which hosts Firebase. At one point, this cable got damaged and interacting with Firebase became very unreliable till the cable was fixed.
This caused events to be emitted to EventStore but updating our readmodels failed.
EvenFlow does offer the functionality to rebuild read models but you need to know which ones are out of sync and be able to rebuild them quickly and you can't rebuild a ReadModel with a certain Id, you have to rebuild all instances of a read model.

To smoothen this process, we have added a back-up store functionality, this will make sure that:

1. You know at all times what each Firebase model should look like as the latest copy is always stored in the back-up store.
2. You know which read models are out of sync as every failure is logged.
3. You can rebuild only the read models that are out of sync. This is very quick as we do not depend on reading events from EventStore and updating the readmodel for each event but merely put the backed up copy there.

The use of the BackUpStore is optional but added benefits you get for free are:
1. Firebase data is not optimized to be queried, you should access data directly by it's node. Having a replica (in MongoDB for now) enables you to index this data and have optimized queries on top of it.
2. For us it saves a roundtrip to the Google datacenter to update a firebase readmodel. The normal process is:
   * Get the read model from firebase
   * Apply the events
   * Put the model back in firebase

The back-up store gets the read model from the back-up store (in MongoDB for now) which runs locally on our server instead of firebase.

#### Firebase only setup

#### Firebase with BackUpStore without [EventFlow.MongoDB](https://github.com/eventflow/EventFlow.MongoDB)

#### Firebase with BackUpStore with [EventFlow.MongoDB](https://github.com/eventflow/EventFlow.MongoDB)

#### FirebaseReadModel configuration

#### FirebaseMappingReadModel configuration