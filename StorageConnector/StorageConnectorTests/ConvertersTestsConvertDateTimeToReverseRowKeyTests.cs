using StorageConnector;
using StorageController;

namespace StorageConnectorTests
{
    [TestClass]
    public class ConvertDateTimeToReverseRowKeyTests
    {
        [TestMethod]
        public void ConvertValidDate()
        {
            // Arrange
            var time = new DateTime(2023, 12, 18, 18, 9, 21, DateTimeKind.Utc);

            // Act
            var result = Converters.ConvertDateTimeToReverseRowKey(time);

            // Assert
            Assert.AreEqual("79768781819078", result);
        }
    }
}