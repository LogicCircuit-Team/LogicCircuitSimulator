namespace CircuitApi.Contracts
{

    public sealed class SaveCircuitRequest
    {
        public string Name {  get; set; }=string.Empty;
        public string JsonData { get; set; } = string.Empty;
    }
}
