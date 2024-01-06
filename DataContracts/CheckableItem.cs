using System.Drawing;

namespace DataContracts
{
    public class DataSeriesMetaData
    {
        public string Id { get; set; }
        public string PartitionKey { get; set; }
        public string Label { get; set; }
        public bool IsChecked { get; set; }
        public string Color { get; set; }
    }
}
