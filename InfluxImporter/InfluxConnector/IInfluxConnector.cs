using InfluxDB.Client.Core.Flux.Domain;

namespace InfluxConnector
{
    public interface IInfluxConnector
    {
        void EnsureBucketExists(string bucketName);
        Task<List<FluxTable>> QueryDataAsync(string query);
        void WritePointDataToInfluxDb(string bucketName, string measurementName, IDictionary<string, object> fields, IDictionary<string, string> tags);
    }
}