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
            var result = AggregationCalculation.GetValueAtTopOfEveryHour("UnitTestMinuteData", "sensor1", new DateTime(2023, 12, 26, 12, 55, 0)).Result;
            result.Should().HaveCount(0);
        }

        [TestMethod]
        public void GetSingleHours()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour("UnitTestMinuteData", "sensor1", new DateTime(2023, 12, 26, 11, 55, 0)).Result;
            result.Should().HaveCount(1);
            result[0].Value.Should().Be(14.14);
        }

        [TestMethod]
        public void GetMultipleHours()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour("UnitTestMinuteData", "sensor1", new DateTime(2023, 12, 26, 10, 55, 0)).Result;
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(3.3);
            result[1].Value.Should().Be(14.14);
        }

        [TestMethod]
        public void GetMultipleHoursSensor2()
        {
            var result = AggregationCalculation.GetValueAtTopOfEveryHour("UnitTestMinuteData", "sensor2", new DateTime(2023, 12, 26, 10, 55, 0)).Result;
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(33);
            result[1].Value.Should().Be(141.4);
        }
    }
}