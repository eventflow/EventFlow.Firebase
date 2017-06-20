using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace EventFlow.Firebase.BackupStore.Model
{
    public class ReadModelUpdateFailure
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public DateTime FailedAt { get; set; }
        public DateTime? FixedAt { get; set; }
        public DateTime? LatestAttempt { get; set; }
        public int Attemps { get; set; }
        public string ReadModelDescription { get; set; }
        public string ReadModelId { get; set; }
        public string ReadModelType { get; set; }
        public string FirebaseMethod { get; set; }
    }
}
