
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using System.Runtime.InteropServices;

namespace StorageController
{
    public class CosmosDBController
    {
        private TableClient tableClient;

        public CosmosDBController(string storageUri, string tableName, string accountName, string storageAccountKey)
        {
            var serviceClient = new TableServiceClient(
                new Uri(storageUri),
                new TableSharedKeyCredential(accountName, storageAccountKey));
            serviceClient.CreateTableIfNotExists(tableName);

            tableClient = serviceClient.GetTableClient(tableName);
        }

        public void WriteData(string partitionKey, string rowKey, decimal value, DateTime time)
        {
            // Create new Entity and write to table
            var entity = new TableEntity(partitionKey.Replace("/", "_"), rowKey)
            {
                { "Value", value },
                { "Time", time.ToUniversalTime() }
            };
            tableClient.AddEntity(entity);
        }
    }
}
