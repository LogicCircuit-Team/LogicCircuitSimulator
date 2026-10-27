namespace CircuitApi.Contracts
{
    public class PublishCircuitRequest
    {
        public string Name { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
    }

    public class SharedCircuitResponse
    {
        public int SharedCircuitId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public bool IsMine { get; set; }
    }
}
