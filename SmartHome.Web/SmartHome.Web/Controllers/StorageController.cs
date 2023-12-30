using Azure.Data.Tables;
using DataContracts;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class StorageController : ControllerBase
{
    // POST api/storage/latestvaluesperkey
    [HttpPost("latestvaluesperkey")]
    public ActionResult<List<DataValueTableEntity>> GetLatestValuePerPartitionKey([FromBody]List<string> PartitionKeys)
    {
        var result = new List<DataValueTableEntity>();
        var table = new TableClient(
            new Uri(SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri),
            "SmartHomeClimateRawData",
            new TableSharedKeyCredential("smarthomestorageprod", SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey)
        );

        foreach (var key in PartitionKeys)
        {
            var queryString = $"PartitionKey eq '{key}'";
            var queryResult = table.Query<DataValueTableEntity>(queryString, 1).ToList().FirstOrDefault();
            if (queryResult != null)
                result.Add(queryResult);
        }
        return result;
    }
}