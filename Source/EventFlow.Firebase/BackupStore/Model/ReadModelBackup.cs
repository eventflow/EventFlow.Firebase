namespace EventFlow.Firebase.BackupStore.Model
{
    public class ReadModelBackup<TReadModel>
    {
        public string _id { get; set; }
        public string ReadModelDescription { get; set; }
        public string ReadModelId { get; set; }        
        public TReadModel ReadModel { get; set; }
    }
}
