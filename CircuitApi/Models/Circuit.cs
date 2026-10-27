namespace CircuitApi.Models
{
    public class Circuit
    {
        public int CircuitId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? OwnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
