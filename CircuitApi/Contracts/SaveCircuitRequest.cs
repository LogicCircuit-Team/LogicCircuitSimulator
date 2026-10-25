namespace CircuitApi.Contracts
{
    // Holds the circuit data sent to the API
    public sealed class SaveCircuitRequest 
    {
        public string Name {  get; set; }=string.Empty;
        public string JsonData { get; set; } = string.Empty;
    }
}
