using Azure;
using Azure.Data.Tables;

namespace DataContracts
{
    public class DataValueTableEntity : ITableEntity
    {
        public double Value { get; set; }
        public DateTime Time { get; set; }
        public DateTime LocalTime { get { return Time.ToLocalTime(); }  }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    public class StringDataValueTableEntity : ITableEntity
    {
        public string Value { get; set; }
        public DateTime Time { get; set; }
        public DateTime LocalTime { get { return Time.ToLocalTime(); } }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
