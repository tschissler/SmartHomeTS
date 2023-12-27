
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using DataContracts;
using System.Runtime.InteropServices;

namespace StorageController
{
    public class CosmosDBController
    {
        private TableClient tableClient;
        private TableServiceClient serviceClient;
        private string tableName;

        public CosmosDBController(string storageUri, string tableName, string accountName, string storageAccountKey)
        {
            serviceClient = new TableServiceClient(
                new Uri(storageUri),
                new TableSharedKeyCredential(accountName, storageAccountKey));
            serviceClient.CreateTableIfNotExists(tableName);

            tableClient = serviceClient.GetTableClient(tableName);
            this.tableName = tableName;
        }

        public void WriteData(string partitionKey, string rowKey, double value, DateTime time)
        {
            // Create new Entity and write to table
            var entity = new TableEntity(partitionKey.Replace("/", "_"), rowKey)
            {
                { "Value", value },
                { "Time", time.ToUniversalTime() }
            };
            tableClient.AddEntity(entity);
        }

        public void DeleteData(string partitionKey, string rowKey)
        {
            // Delete entity from table
            tableClient.DeleteEntity(partitionKey, rowKey);
        }

        public DataValueTableEntity ReadTop1Data(string filer)
        {
            // Read data from table
            return tableClient.Query<DataValueTableEntity>(filer, 1).FirstOrDefault();
        }

        public List<DataValueTableEntity> ReadData(string filer)
        {
            // Read data from table
            return tableClient.Query<DataValueTableEntity>(filer).ToList();
        }

        public void ClearTable()
        {
            tableClient.DeleteAsync();
            Thread.Sleep(1000);
            serviceClient.CreateTable(tableName);
            tableClient = serviceClient.GetTableClient(tableName); 
        }
    }
}
