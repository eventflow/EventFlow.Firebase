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
Register Fiebase using one of the following ```IEventFlowOptions``` extension methods:
```c#
public static IEventFlowOptions ConfigureFirebase(this IEventFlowOptions eventFlowOptions, string firebasePath, bool useBackupStore);
public static IEventFlowOptions ConfigureFirebase(this IEventFlowOptions eventFlowOptions, string firebasePath, string firebaseAuthSecret, bool useBackupStore);
```
example:
```c#
return EventFlowOptions.New
    ...
    .ConfigureFirebase("https://<your-firebase-instance>.firebaseio.com/", false)
    ...
    .CreateResolver();
```

Or with authenticating your backend with your database secret:
```c#
return EventFlowOptions.New

    .ConfigureFirebase("https://<your-firebase-instance>.firebaseio.com/", "<your databse secret>", false)

    .CreateResolver();
```

This will only use Firebase and bypass the back-up store.


#### Firebase with BackUpStore without [EventFlow.MongoDB](https://github.com/eventflow/EventFlow.MongoDB)
The only implementation so far for the back-up store is MongoDb, and therefor, ha a dependency on ```IMongoDatabase```
This means that, if you do not use MongoDb yet, you need to configure it as well.

This can be done by one of the following ```IEventFlowOptions``` extension methods:
```c#
public static IEventFlowOptions ConfigureFirebase(this IEventFlowOptions eventFlowOptions, string firebasePath, string mongoDbConnectionString, string mongoDbDatabase);
public static IEventFlowOptions ConfigureFirebase(this IEventFlowOptions eventFlowOptions, string firebasePath, string firebaseAuthSecret, string mongoDbConnectionString, string mongoDbDatabase);
```
example:
```c#
return EventFlowOptions.New
    ...
    .ConfigureFirebase("https://<your-firebase-instance>.firebaseio.com/", "mongodb://localhost:27017", "<mongoDb name>")
    ...
    .CreateResolver();
```

Or with authenticating your backend with your database secret:

```c#
return EventFlowOptions.New
    ...
    .ConfigureFirebase("https://<your-firebase-instance>.firebaseio.com/", "<your databse secret>", "mongodb://localhost:27017", "<mongoDb name>")
    ...
    .CreateResolver();
```

#### Firebase with BackUpStore with [EventFlow.MongoDB](https://github.com/eventflow/EventFlow.MongoDB)
If you are already using MongoDb with EventFlow and using the EventFlow.MongoDb package, we setup a ```IMongoDatabase``` registration during the ```ConfigureMongoDb()``` call so we don not need to tell the fireabse configuration about the MongoDb anymore. 

IMPORTANT: configure MongoDb before configuring Firebase!
example:
```c#
return EventFlowOptions.New
    ...
    .ConfigureMongoDb("<mongo connection string>", "<mongo database name>")
    ...
    .ConfigureFirebase("https://<your-firebase-instance>.firebaseio.com/", true)
    ...
    .CreateResolver();
```

Or with authenticating your backend with your database secret:
```c#
return EventFlowOptions.New
    ...
    .ConfigureMongoDb("<mongo connection string>", "<mongo database name>")
    ...
    .ConfigureFirebase("https://<your-firebase-instance>.firebaseio.com/", "<your databse secret>", true)
    ...
    .CreateResolver();
```
#### FirebaseReadModel configuration
Configuring normal Firebase readmodels is identical to other EventFlow readmodels. You only need to implement ```IFirebaseReadModel``` instead of ```IReadModel```.

example:
```c#
[FirebaseNodeName("courses")]
public class CourseReadModel : IFirebaseReadModel,
    IAmReadModelFor<CourseAggregate, CourseId, CourseCreated>
{
    public long? _version { get; set; }
    public string Name { get; set; }
    public DateTime Created { get; set; }
    public string Level { get; set; }

    public void Apply(
        IReadModelContext context,
        IDomainEvent<CourseAggregate, CourseId, CourseCreated> domainEvent)
    {
        Name = domainEvent.AggregateEvent.Name;
        Created = domainEvent.AggregateEvent.Created;
        Level = domainEvent.AggregateEvent.Level;
    }
}
```

Simular to the normal IReadModel, it can use a locator according to your needs, configuring a FirebaseReadModel would like:
```c#
return EventFlowOptions.New
    ...
    .UseFirebaseReadModel<CourseReadModel>()
    ...
    .CreateResolver();
```
Or with a locator:
```c#
return EventFlowOptions.New
    ...
    .RegisterServices(sr =>
    {
        sr.Register<ICourseReadModelLocator, CourseReadModelLocator>();
    })
    ...
    .UseFirebaseReadModel<CourseReadModel, ICourseReadModelLocator>()
    ...
    .CreateResolver();
```

#### FirebaseMappingReadModel configuration
##### Why mapping read models?
As mentioned before in this ReadMe, Firebase isn't optimized to query your data but instead prefers you to go to nodes directly.
Let's say we have a model like below:

![Model](readme1.png)

If you want to retrieve all lessons for course course-78016fce-0923-41cf-abe3-556dba2c7997, you can query the lessons node for all children where CourseId = course-78016fce-0923-41cf-abe3-556dba2c7997.
The Firebase docs do not encourage this approach because of performance. They encourage you to create 'mapping' nodes like below:

![Mappings](readme2.png)

This enables us to get all the lesson id's for course course-78016fce-0923-41cf-abe3-556dba2c7997 by doing a simple get to /course-lessons/course-78016fce-0923-41cf-abe3-556dba2c7997.

Your front end should use observables to retrieve Firebase data because of it's realtime nature and all the rx libraries (like RXJS) have powerfull operators to fetch the ids, map and combine the fetching for the lessons and return a nice mapping for all the lessons for this course in a a couple of lines of code.

##### Configuring mapping read models

Configuring Firebase mapping readmodels is also identical to other EventFlow readmodels. You only need to implement ```IFirebaseMappingReadModel``` instead of ```IReadModel```.
The only property used on the read model is the ```Dictionary<string, bool> Children { get; set; }``` to hold the actual mappings.

example:
```c#
[FirebaseNodeName("courses")]
public class CourseReadModel : IFirebaseMappingReadModel,
    IAmReadModelFor<CourseAggregate, CourseId, CourseCreated>
{
    public long? _version { get; set; }
    public string Name { get; set; }
    public DateTime Created { get; set; }
    public string Level { get; set; }

    public void Apply(
        IReadModelContext context,
        IDomainEvent<CourseAggregate, CourseId, CourseCreated> domainEvent)
    {
        Name = domainEvent.AggregateEvent.Name;
        Created = domainEvent.AggregateEvent.Created;
        Level = domainEvent.AggregateEvent.Level;
    }
}
```

Simular to the normal IReadModel, it can use a locator according to your needs, configuring a FirebaseReadModel would like:
```c#
return EventFlowOptions.New
    ...
    .UseFirebaseMappingReadModel<CourseLessonsMappingReadModel>()
    ...
    .CreateResolver();
```
Or with a locator:
```c#
return EventFlowOptions.New
    ...
    .RegisterServices(sr =>
    {
        sr.Register<ICourseLessonsMappingReadModelLocator, CourseLessonsMappingReadModelLocator>();
    })
    ...
    .UseFirebaseReadModel<CourseLessonsMappingReadModel, ICourseLessonsMappingReadModelLocator>()
    ...
    .CreateResolver();
```

### A Complete example - Course - Lessons
I'm excluding the actual aggregates, commands, handlers, etc. The EventFlow's documentation habdles that.

I'll just list the events which are emitted and used by the read models.

#### Events
```c#
public class CourseCreated
        : AggregateEvent<CourseAggregate, CourseId>
{
    public CourseCreated(
        string name,
        DateTime created,
        string level)
    {
        Name = name;
        Created = created;
        Level = level;
    }

    public string Name { get; set; }
    public DateTime Created { get; set; }
    public string Level { get; set; }
    }
```

```c#
public class LessonCreated : AggregateEvent<LessonAggregate, LessonId>
{
    public LessonCreated(string name, string courseId, decimal cost, string author, int hours)
    {
        Name = name;
        CourseId = courseId;
        Cost = cost;
        Author = author;
        Hours = hours;
    }

    public string Name { get; set; }
    public string CourseId { get; set; }
    public decimal Cost { get; set; }
    public string Author { get; set; }
    public int Hours { get; set; }
}
```

The FirebaseReadModels
```c#
[FirebaseNodeName("courses")]
public class CourseReadModel : IFirebaseReadModel,
    IAmReadModelFor<CourseAggregate, CourseId, CourseCreated>
{
    public long? _version { get; set; }
    public string Name { get; set; }
    public DateTime Created { get; set; }
    public string Level { get; set; }
    public void Apply(
        IReadModelContext context,
        IDomainEvent<CourseAggregate, CourseId, CourseCreated> domainEvent)
    {
        Name = domainEvent.AggregateEvent.Name;
        Created = domainEvent.AggregateEvent.Created;
        Level = domainEvent.AggregateEvent.Level;
    }
}
```

```c#
[FirebaseNodeName("lessons")]
public class LessonReadModel : IFirebaseReadModel,
    IAmReadModelFor<LessonAggregate, LessonId, LessonCreated>
{
    public long? _version { get; set; }
    public string Name { get; set; }
    public string CourseId { get; set; }
    public decimal Cost { get; set; }
    public string Author { get; set; }
    public int Hours { get; set; }

    public void Apply(IReadModelContext context, IDomainEvent<LessonAggregate, LessonId, LessonCreated> domainEvent)
    {
        Name = domainEvent.AggregateEvent.Name;
        Cost = domainEvent.AggregateEvent.Cost;
        Author = domainEvent.AggregateEvent.Author;
        Hours = domainEvent.AggregateEvent.Hours;
        CourseId = domainEvent.AggregateEvent.CourseId;
    }
    }
```

The FirebaseMappingReadModel

```c#
[FirebaseNodeName("course-lessons")]
public class CourseLessonsMappingReadModel : IFirebaseMappingReadModel,
    IAmReadModelFor<LessonAggregate, LessonId, LessonCreated>
{
    public Dictionary<string, bool> Children { get; set; }

    public void Apply(IReadModelContext context, IDomainEvent<LessonAggregate, LessonId, LessonCreated> domainEvent)
    {
        if (Children == null)
            Children = new Dictionary<string, bool>();

        Children.Add(domainEvent.AggregateIdentity.Value, true);
    }
}
```

The FirebaseMappingReadModelLocator
```c#
public interface ICourseLessonsMappingReadModelLocator : IReadModelLocator { }

public class CourseLessonsMappingReadModelLocator : ICourseLessonsMappingReadModelLocator
{
    public IEnumerable<string> GetReadModelIds(IDomainEvent domainEvent)
    {
        if (domainEvent.EventType == typeof(LessonCreated))
            yield return ((LessonCreated)domainEvent.GetAggregateEvent()).CourseId;
    }
}
```