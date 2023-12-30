using DataAggregatorFunctions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DataAggregatorTests
{
    [TestClass]
    public class GetValueAtTopOfEveryHourTests
    {
        [TestMethod]
        public void GetNoHours()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour("UnitTestMinuteData", "sensor1", new DateTime(2023, 12, 28, 19, 55, 0, DateTimeKind.Utc)).Result;
            result.Should().HaveCount(0);
        }

        [TestMethod]
        public void GetSingleHours()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData",
                "sensor1",
                new DateTime(2023, 12, 28, 11, 55, 0, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 12, 30, 0, DateTimeKind.Utc))
                .Result;

            result.Should().HaveCount(1);
            result[0].Value.Should().Be(13.13);
        }

        [TestMethod]
        public void GetMultipleHoursSensor1()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData", 
                "sensor1", 
                new DateTime(2023, 12, 28, 10, 55, 0, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 12, 30, 0, DateTimeKind.Utc))
                .Result;
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(2.2);
            result[1].Value.Should().Be(13.13);
        }

        [TestMethod]
        public void GetMultipleHoursWithMissingDataAtEnd()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData",
                "sensor1",
                new DateTime(2023, 12, 28, 10, 55, 0, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 14, 30, 0, DateTimeKind.Utc))
                .Result;
            result.Should().HaveCount(3);
            result[0].Value.Should().Be(2.2);
            result[1].Value.Should().Be(13.13);
            result[2].Value.Should().Be(17.17);
        }

        [TestMethod]
        public void GetMultipleHoursWithDataGap()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData",
                "sensor1",
                new DateTime(2023, 12, 28, 11, 55, 0, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 19, 30, 0, DateTimeKind.Utc))
                .Result;
            result.Should().HaveCount(3);
            result[0].Value.Should().Be(13.13);
            result[1].Value.Should().Be(17.17);
            result[2].Value.Should().Be(115.15);
        }

        [TestMethod]
        public void GetLastDataPointWhenMissingDataAtEnd()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData",
                "sensor1",
                new DateTime(2023, 12, 28, 19, 00, 0, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 22, 10, 0, DateTimeKind.Utc))
                .Result;
            result.Should().HaveCount(1);
            result[0].Value.Should().Be(117.17);
        }

        [TestMethod]
        public void GetNoDataWhenLastDateExceedsLastDataPoint()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData",
                "sensor1",
                new DateTime(2023, 12, 28, 19, 30, 0, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 22, 10, 0, DateTimeKind.Utc))
                .Result;
            result.Should().HaveCount(0);
        }

        [TestMethod]
        public void GetMultipleHoursSensor2()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData",
                "sensor2",
                new DateTime(2023, 12, 28, 10, 55, 0, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 12, 30, 0, DateTimeKind.Utc))
                .Result;
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(22);
            result[1].Value.Should().Be(131.3);
        }

        [TestMethod]
        public void GetDataAfter1110Before2230UTC()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData",
                "sensor1",
                new DateTime(2023, 12, 28, 11, 10, 45, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 22, 30, 0, DateTimeKind.Utc))
                .Result;
            result.Should().HaveCount(4);
            result[0].Value.Should().Be(13.13);
            result[1].Value.Should().Be(17.17);
            result[2].Value.Should().Be(115.15);
            result[3].Value.Should().Be(117.17);
        }

        [TestMethod]
        public void GetDataAfter1110Before1930UTC()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour(
                "UnitTestMinuteData",
                "sensor1",
                new DateTime(2023, 12, 28, 11, 10, 45, DateTimeKind.Utc),
                new DateTime(2023, 12, 28, 19, 30, 0, DateTimeKind.Utc))
                .Result;
            result.Should().HaveCount(3);
            result[0].Value.Should().Be(13.13);
            result[1].Value.Should().Be(17.17);
            result[2].Value.Should().Be(115.15);
        }
    }
}