using DataAggregator;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DataAggregatorTests
{
    [TestClass]
    public class CalculateTopOfTheHourTests
    {
        [TestMethod]
        public void CalculateLocalTime()
        {
            var result = AggregationCalculation.CalculateTopOfTheHour(new DateTime(2023, 12, 18, 11, 12, 3));
            result.Should().Be(new DateTime(2023, 12, 18, 12, 0, 0));
        }

        [TestMethod]
        public void CalculateUTCTime()
        {
            var result = AggregationCalculation.CalculateTopOfTheHour(new DateTime(2023, 12, 18, 11, 12, 3, DateTimeKind.Utc));
            result.Should().Be(new DateTime(2023, 12, 18, 12, 0, 0, DateTimeKind.Utc));
            result.Kind.Should().Be(DateTimeKind.Utc);
        }
    }
}