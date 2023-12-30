using Microsoft.VisualStudio.TestTools.UnitTesting;
using DataAggregatorFunctions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DataAggregatorFunctions.Tests
{
    [TestClass()]
    public class AggregationExecutionTests
    {
        [TestMethod()]
        [TestCategory("ManualOnly")]
        public void AggregateClimateDataTest()
        {
            AggregationExecution.AggregateClimateData(
                LoggerFactory.Create(
                    builder =>
                    {
                        builder.AddConsole();
                    })
                .CreateLogger("AggregationExecutionTests")
                );
        }
    }
}