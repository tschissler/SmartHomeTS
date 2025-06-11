using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core;
using InfluxDB.Client.Core.Flux.Domain;
using InfluxDB.Client.Writes;
using Newtonsoft.Json.Linq;
using SmartHomeHelpers.Logging;
using System.Diagnostics;

namespace InfluxConnector
{
    public class InfluxDbConnector : IInfluxDBConnector
    {
        private readonly WriteApi _writeApi;
        private readonly BucketsApi _bucketsApi;
        private readonly QueryApi _queryApi;
        private readonly OrganizationsApi _organizationsApi;
        private readonly string _org;
        private string _orgId;

        public InfluxDbConnector(string influxUrl, string org, string token)
        {
            this._org = org;

            var options = InfluxDBClientOptions.Builder
               .CreateNew()
               .Url(influxUrl)
               .Org(org)
               .AuthenticateToken(token.ToCharArray())
               .LogLevel(InfluxDB.Client.Core.LogLevel.None)
               .Build();
            var influxDbClient = new InfluxDBClient(options);
            _organizationsApi = influxDbClient.GetOrganizationsApi();
            _writeApi = influxDbClient.GetWriteApi();
            _bucketsApi = influxDbClient.GetBucketsApi();
            _queryApi = influxDbClient.GetQueryApi();

            var organizations = _organizationsApi.FindOrganizationsAsync().GetAwaiter().GetResult();
            var organization = organizations.FirstOrDefault(i => i.Name == org);
            if (organization == null)
            {
                throw new Exception($"Organization '{org}' not found.");
            }

            _orgId = organization.Id;
        }

        public void EnsureBucketExists(string bucketName)
        {
            var existingBucket = _bucketsApi.FindBucketByNameAsync(bucketName).Result;

            if (existingBucket == null)
            {
                _bucketsApi.CreateBucketAsync(bucketName, new BucketRetentionRules(BucketRetentionRules.TypeEnum.Expire, 0), _orgId);
                ConsoleHelpers.PrintInformation($"Bucket '{bucketName}' created in Org {_org}.");
            }
        }

        public void WritePointDataToInfluxDb(
            string bucketName,
            string measurementName,
            IDictionary<string, object> fields,
            IDictionary<string, string> tags)
        {
            // Ensure the bucket exists before writing data
            EnsureBucketExists(bucketName);

            // Build the InfluxDB point
            var point = PointData.Measurement(measurementName)
                .Timestamp(DateTime.UtcNow, WritePrecision.Ns)
                .SetFields(fields)
                .SetTags(tags);

            // Write the point to InfluxDB
            _writeApi.WritePoint(point, bucketName);
            //ConsoleHelpers.PrintInformation($"Data written to InfluxDB bucket '{bucketName}'");
        }

        public async Task<List<FluxTable>> QueryDataAsync(string query)
        {
            var fluxTables = await _queryApi.QueryAsync(query, _org);
            return fluxTables;
        }
    }
}
