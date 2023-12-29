using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DataAggregator
{
    public class AggregateClimateData
    {
        [FunctionName("AggregateClimateHourly")]
        public void Run([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger log)
        {
            AggregationExecution.AggregateClimateData();
            log.LogInformation($"AggregateClimateHourly executed at: {DateTime.Now}");
        }
    }
}
