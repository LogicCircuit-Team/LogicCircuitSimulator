namespace CircuitApi.Models
{
    public class SharedCircuit
    {
        public int SharedCircuitId { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
    }
}
