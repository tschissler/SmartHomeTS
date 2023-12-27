using StorageConnector;

namespace StorageConnectorTests
{
    [TestClass]
    public class ConvertersTestsConvertDateTimeToReverseRowKeyTests
    {
        [TestMethod]
        public void ConvertValidDate()
        {
            // Arrange
            var time = new DateTime(2023, 12, 18, 19, 9, 21);

            // Act
            var result = Converters.ConvertDateTimeToReverseRowKey(time);

            // Assert
            Assert.AreEqual("79768781809078", result);
        }
    }
}