using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using System.Collections.Generic;
using System.Linq;
using System;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class InfluxDBClient
{
    private static readonly string INFLUXDB_URL = Environment.GetEnvironmentVariable("INFLUXDB_URL") ?? "http://localhost:8086";
    private static readonly string INFLUXDB_TOKEN = Environment.GetEnvironmentVariable("INFLUXDB_TOKEN") ?? "";
    private static readonly string INFLUXDB_ORG = Environment.GetEnvironmentVariable("INFLUXDB_ORG") ?? "SmartHome";

    [McpServerTool, Description("Returns a list of all available bucket names from the InfluxDB database.")]
    public static async Task<string> GetBuckets()
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var bucketsApi = client.GetBucketsApi();
            var buckets = await bucketsApi.FindBucketsAsync();
            
            var bucketNames = buckets.Select(b => b.Name).ToList();
            return JsonSerializer.Serialize(bucketNames);
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Returns all measurements in a specific bucket.")]
    public static async Task<string> GetMeasurements(string bucket)
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var queryApi = client.GetQueryApi();
            
            // Build the Flux query to get all measurements for the bucket
            var query = $"import \"influxdata/influxdb/schema\"\nschema.measurements(bucket: \"{bucket}\")";
            
            var tables = await queryApi.QueryAsync(query, INFLUXDB_ORG);
            var measurements = new List<string>();
            
            if (tables.Count > 0 && tables[0].Records.Count > 0)
            {
                measurements = tables[0].Records
                    .Select(r => r.GetValue().ToString())
                    .ToList();
            }
            
            return JsonSerializer.Serialize(measurements);
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Returns all field keys for a given measurement in a bucket.")]
    public static async Task<string> GetFields(string bucket, string measurement)
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var queryApi = client.GetQueryApi();
            
            // Build the Flux query to get all field keys for the measurement
            var query = $"import \"influxdata/influxdb/schema\"\nschema.fieldKeys(bucket: \"{bucket}\", predicate: (r) => r._measurement == \"{measurement}\")";
            
            var tables = await queryApi.QueryAsync(query, INFLUXDB_ORG);
            var fields = new List<string>();
            
            if (tables.Count > 0 && tables[0].Records.Count > 0)
            {
                fields = tables[0].Records
                    .Select(r => r.GetValue().ToString())
                    .ToList();
            }
            
            return JsonSerializer.Serialize(fields);
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Returns all tag keys for a given measurement in a bucket.")]
    public static async Task<string> GetTags(string bucket, string measurement)
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var queryApi = client.GetQueryApi();
            
            // Build the Flux query to get all tag keys for the measurement
            var query = $"import \"influxdata/influxdb/schema\"\nschema.tagKeys(bucket: \"{bucket}\", predicate: (r) => r._measurement == \"{measurement}\")";
            
            var tables = await queryApi.QueryAsync(query, INFLUXDB_ORG);
            var tags = new List<string>();
            
            if (tables.Count > 0 && tables[0].Records.Count > 0)
            {
                tags = tables[0].Records
                    .Select(r => r.GetValue().ToString())
                    .ToList();
            }
            
            return JsonSerializer.Serialize(tags);
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Returns all possible values for a specific tag key in a measurement.")]
    public static async Task<string> GetTagValues(string bucket, string measurement, string tag)
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var queryApi = client.GetQueryApi();
            
            // Build the Flux query to get all values for the tag
            var query = $"import \"influxdata/influxdb/schema\"\nschema.tagValues(bucket: \"{bucket}\", predicate: (r) => r._measurement == \"{measurement}\", tag: \"{tag}\")";
            
            var tables = await queryApi.QueryAsync(query, INFLUXDB_ORG);
            var values = new List<string>();
            
            if (tables.Count > 0 && tables[0].Records.Count > 0)
            {
                values = tables[0].Records
                    .Select(r => r.GetValue().ToString())
                    .ToList();
            }
            
            return JsonSerializer.Serialize(values);
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Returns the data types of each field in a measurement (if available).")]
    public static async Task<string> GetFieldTypes(string bucket, string measurement)
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var queryApi = client.GetQueryApi();
            
            // Build the Flux query to get field keys and types
            var query = $"import \"influxdata/influxdb/schema\"\nschema.fieldKeys(bucket: \"{bucket}\", predicate: (r) => r._measurement == \"{measurement}\")";
            
            var tables = await queryApi.QueryAsync(query, INFLUXDB_ORG);
            var fieldTypes = new List<FieldType>();
            
            if (tables.Count > 0 && tables[0].Records.Count > 0)
            {
                foreach (var record in tables[0].Records)
                {
                    string field = record.GetValue().ToString();
                    string type = "unknown";
                    
                    // Try to get the type if available
                    if (record.Values.ContainsKey("type"))
                    {
                        type = record.Values["type"]?.ToString() ?? "unknown";
                    }
                    
                    fieldTypes.Add(new FieldType { Field = field, Type = type });
                }
            }
            
            return JsonSerializer.Serialize(fieldTypes);
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Returns the earliest and latest timestamps for a measurement within the specified days.")]
    public static async Task<string> GetTimeRange(string bucket, string measurement, int days = 365)
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var queryApi = client.GetQueryApi();
            
            // Query for min _time
            var queryMin = $"from(bucket: \"{bucket}\")\n" +
                          $"  |> range(start: -{days}d)\n" +
                          $"  |> filter(fn: (r) => r._measurement == \"{measurement}\")\n" +
                          "  |> keep(columns: [\"_time\"])\n" +
                          "  |> min(column: \"_time\")\n";
            
            var minTables = await queryApi.QueryAsync(queryMin, INFLUXDB_ORG);
            string? minTime = null;
            
            if (minTables.Count > 0 && minTables[0].Records.Count > 0)
            {
                minTime = minTables[0].Records[0].GetTimeInDateTime().ToString();
            }
            
            // Query for max _time
            var queryMax = $"from(bucket: \"{bucket}\")\n" +
                          $"  |> range(start: -{days}d)\n" +
                          $"  |> filter(fn: (r) => r._measurement == \"{measurement}\")\n" +
                          "  |> keep(columns: [\"_time\"])\n" +
                          "  |> max(column: \"_time\")\n";
            
            var maxTables = await queryApi.QueryAsync(queryMax, INFLUXDB_ORG);
            string? maxTime = null;
            
            if (maxTables.Count > 0 && maxTables[0].Records.Count > 0)
            {
                maxTime = maxTables[0].Records[0].GetTimeInDateTime().ToString();
            }
            
            var result = new TimeRange
            {
                MinTime = minTime,
                MaxTime = maxTime,
                WindowDays = days
            };
            
            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            // Check for timeout
            if (ex.Message.Contains("timeout"))
            {
                return JsonSerializer.Serialize(new 
                { 
                    error = "Query timed out. Try a smaller bucket, measurement, or shorter time window." 
                });
            }
            
            // Log and return error information
            return JsonSerializer.Serialize(new 
            { 
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    [McpServerTool, Description("Returns the version of the connected InfluxDB instance.")]
    public static async Task<string> GetInfluxDBVersion()
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var health = await client.HealthAsync();
            
            string? version = health?.Version;
            
            return JsonSerializer.Serialize(new { Version = version });
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Reads the schema from the InfluxDB database and returns it as a JSON string.")]
    public static async Task<string> GetSchema()
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var bucketsApi = client.GetBucketsApi();
            var buckets = await bucketsApi.FindBucketsAsync();
            
            var result = new List<BucketInfo>();
            var queryApi = client.GetQueryApi();

            foreach (var bucket in buckets)
            {
                var bucketName = bucket.Name;
                
                // Get measurements for this bucket
                var measurementsQuery = $"import \"influxdata/influxdb/schema\"\nschema.measurements(bucket: \"{bucketName}\")";
                var measurementsFlux = await queryApi.QueryAsync(measurementsQuery, INFLUXDB_ORG);
                
                // Extract measurement names if the result is not empty
                var measurements = new List<string>();
                if (measurementsFlux.Count > 0 && measurementsFlux[0].Records.Count > 0)
                {
                    measurements = measurementsFlux[0].Records
                        .Select(r => r.GetValue().ToString())
                        .ToList();
                }
                
                var bucketInfo = new BucketInfo 
                { 
                    Bucket = bucketName,
                    Measurements = new List<MeasurementInfo>() 
                };
                
                foreach (var measurement in measurements)
                {
                    // Get fields using predicate
                    var fieldsQuery = $"import \"influxdata/influxdb/schema\"\nschema.fieldKeys(bucket: \"{bucketName}\", predicate: (r) => r._measurement == \"{measurement}\")";
                    var fieldsFlux = await queryApi.QueryAsync(fieldsQuery, INFLUXDB_ORG);
                    
                    // Extract field names if the result is not empty
                    var fields = new List<string>();
                    if (fieldsFlux.Count > 0 && fieldsFlux[0].Records.Count > 0)
                    {
                        fields = fieldsFlux[0].Records
                            .Select(r => r.GetValue().ToString())
                            .ToList();
                    }
                    
                    // Get tags using predicate
                    var tagsQuery = $"import \"influxdata/influxdb/schema\"\nschema.tagKeys(bucket: \"{bucketName}\", predicate: (r) => r._measurement == \"{measurement}\")";
                    var tagsFlux = await queryApi.QueryAsync(tagsQuery, INFLUXDB_ORG);
                    
                    // Extract tag names if the result is not empty
                    var tags = new List<string>();
                    if (tagsFlux.Count > 0 && tagsFlux[0].Records.Count > 0)
                    {
                        tags = tagsFlux[0].Records
                            .Select(r => r.GetValue().ToString())
                            .ToList();
                    }
                    
                    bucketInfo.Measurements.Add(new MeasurementInfo
                    {
                        Measurement = measurement,
                        Fields = fields,
                        Tags = tags
                    });
                }
                
                result.Add(bucketInfo);
            }
            
            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Executes a query on the InfluxDB database and returns 'Is OK' if successful or the error message if it fails.")]
    public static async Task<string> ExecuteQuery(string query)
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var queryApi = client.GetQueryApi();

            // Execute the query
            await queryApi.QueryAsync(query, INFLUXDB_ORG);

            // If no exception is thrown, the query is OK
            return JsonSerializer.Serialize(new { result = "Is OK" });
        }
        catch (Exception ex)
        {
            // Return the error message
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }


    [McpServerTool, Description("Return recent sample data for a specific bucket and measurement.")]
    public static async Task<string> GetSample(string bucket, string measurement, int limit = 5)
    {
        try
        {
            var options = new InfluxDBClientOptions.Builder()
                .Url(INFLUXDB_URL)
                .AuthenticateToken(INFLUXDB_TOKEN)
                .Org(INFLUXDB_ORG)
                .Build();

            using var client = new InfluxDB.Client.InfluxDBClient(options);
            var queryApi = client.GetQueryApi();
            
            // Build the Flux query to get recent samples
            var query = $"from(bucket: \"{bucket}\")\n" +
                        $"  |> range(start: -1d)\n" +
                        $"  |> filter(fn: (r) => r._measurement == \"{measurement}\")\n" +
                        $"  |> limit(n: {limit})";
            
            var tables = await queryApi.QueryAsync(query, INFLUXDB_ORG);
            var samples = new List<Dictionary<string, object>>();
            
            foreach (var table in tables)
            {
                foreach (var record in table.Records)
                {
                    // Convert the record values to a dictionary
                    var recordValues = new Dictionary<string, object>();
                    
                    // Add all fields from the record
                    foreach (var key in record.Values.Keys)
                    {
                        recordValues[key] = record.Values[key];
                    }
                    
                    samples.Add(recordValues);
                }
            }
            
            return JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            // Log and return error information
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
    
    // Helper classes for serialization
    private class BucketInfo
    {
        public string Bucket { get; set; }
        public List<MeasurementInfo> Measurements { get; set; }
    }
    
    private class MeasurementInfo
    {
        public string Measurement { get; set; }
        public List<string> Fields { get; set; }
        public List<string> Tags { get; set; }
    }
    
    private class FieldType
    {
        public string Field { get; set; }
        public string Type { get; set; }
    }
    
    private class TimeRange
    {
        public string? MinTime { get; set; }
        public string? MaxTime { get; set; }
        public int WindowDays { get; set; }
    }
}