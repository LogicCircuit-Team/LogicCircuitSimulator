namespace CircuitApi.Contracts
{
    public sealed class HdlSynthesisResponse
    {
        public string NetlistJson { get; set; } = string.Empty;
        public string Diagnostics {  get; set; } = string.Empty;
    }
}
