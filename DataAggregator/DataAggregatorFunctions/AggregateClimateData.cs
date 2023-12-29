using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DataAggregatorFunctions
{
    public class AggregateClimateData
    {
        private readonly ILogger _logger;

        public AggregateClimateData(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AggregateClimateData>();
        }

        [Function("AggregateClimateHourly")]
        public void Run([TimerTrigger("0 0 * * * *")] MyInfo myTimer)
        {
            _logger.LogInformation($"AggregateClimateHourly was trigger at: {DateTime.Now}");
            try
            {
                AggregationExecution.AggregateClimateData(_logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
