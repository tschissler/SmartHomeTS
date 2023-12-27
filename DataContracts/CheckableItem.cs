namespace DataContracts
{
    public class CheckableItem
    {
        public string PartitionKey { get; set; }
        public string Label { get; set; }
        public bool IsChecked { get; set; }
    }
}
