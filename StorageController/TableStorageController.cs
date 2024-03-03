
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using DataContracts;
using System.Runtime.InteropServices;

namespace StorageController
{
    public class TableStorageController
    {
        private TableClient tableClient;
        private TableServiceClient serviceClient;
        private string tableName;
        public string TableName { get { return tableName; } }

        public TableStorageController(string storageUri, string tableName, string accountName, string storageAccountKey)
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

        public void WriteData(string partitionKey, string rowKey, string value, DateTime time)
        {
            // Create new Entity and write to table
            var entity = new TableEntity(partitionKey.Replace("/", "_"), rowKey)
            {
                { "Value", value },
                { "Time", time.ToUniversalTime() }
            };
            tableClient.AddEntity(entity);
        }

        public void WriteData(DataValueTableEntity item)
        {
            tableClient.AddEntity(item);
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
            tableClient.Delete();
            while (true)
            {
                try
                { 
                    Thread.Sleep(5000);
                    serviceClient.CreateTable(tableName);
                    break;
                }
                catch (Exception)
                {
                }
            }
            tableClient = serviceClient.GetTableClient(tableName); 
        }

        public DataValueTableEntity GetNewestItem(string partitionKey)
        {
            if (ReadTop1Data($"PartitionKey eq '{partitionKey}'") == null)
            {
                return null;
            }
            var startTime = DateTime.Now.AddHours(-1);
            var items = tableClient.Query<DataValueTableEntity>($"PartitionKey eq '{partitionKey}' and Time ge datetime'{startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}'");
            while (items.Count() == 0)
            {
                startTime = startTime.AddHours(-1);
                items = tableClient.Query<DataValueTableEntity>($"PartitionKey eq '{partitionKey}' and Time ge datetime'{startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}'");
            }

            return items.OrderByDescending(i => i.Time).FirstOrDefault();
        }
    }
}
