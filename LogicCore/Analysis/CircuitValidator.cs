using LogicCore.Interfaces;
using LogicCore.Management;

namespace LogicCore.Analysis
{
    internal static class CircuitValidator
    {
        internal static void Validate(CircuitManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);
            var result = manager.TryValidate();
            if (!result.IsValid)
                throw new InvalidOperationException(result.Message);
            var connected = new Dictionary<IGate, bool[]>(manager.Gates.Count, ReferenceEqualityComparer.Instance);
            foreach (var gate in manager.Gates)
                connected.Add(gate, new bool[gate.Inputs.Length]);
            foreach (var wire in manager.Wires)
                connected[wire.TargetGate][wire.TargetInputIndex] = true;
            foreach (var gate in manager.Gates)
                for (int inputIndex = 0; inputIndex < gate.Inputs.Length; inputIndex++)
                    if (!connected[gate][inputIndex])
                        throw new InvalidOperationException($"Input {inputIndex} on gate '{gate.Id}' is not connected.");
        }
    }
}
