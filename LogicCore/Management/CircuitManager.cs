using LogicCore.Connections;
using LogicCore.Interfaces;
using LogicCore.Validation;
using ValidationResult = LogicCore.Validation.CircuitValidator.ValidationResult;

namespace LogicCore.Management
{
    public class CircuitManager
    {
        public List<IGate> Gates { get; } = new();
        public List<Wire> Wires { get; } = new();
        public bool HasFeedbackLoop { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        private IGate[]? evaluationOrder;
        private Dictionary<IGate, List<Wire>> incomingWires = new(ReferenceEqualityComparer.Instance);
        private IGate[] snapshotGates = Array.Empty<IGate>();
        private Wire[] snapshotWires = Array.Empty<Wire>();
        private string[] snapshotIds = Array.Empty<string>();
        private string[] snapshotTypes = Array.Empty<string>();
        private bool[][] snapshotInputs = Array.Empty<bool[]>();

        public void AddGate(IGate gate)
        {
            var result = TryAddGate(gate);
            if (!result.IsValid)
                throw new InvalidOperationException(result.Message);
        }

        public ValidationResult TryAddGate(IGate? gate)
        {
            var result = CircuitValidator.CanAddGate(this, gate);
            if (!result.IsValid)
                return RecordResult(result);
            Gates.Add(gate!);
            InvalidatePlan();
            return RecordResult(ValidationResult.Success());
        }

        public void AddWire(Wire wire)
        {
            var result = TryAddWire(wire);
            if (!result.IsValid)
                throw new InvalidOperationException(result.Message);
        }

        public ValidationResult TryAddWire(Wire? wire)
        {
            if (wire == null)
                return RecordResult(ValidationResult.Fail("A wire is required."));
            var result = CircuitValidator.CanConnect(this, wire.SourceGate, wire.TargetGate, wire.TargetInputIndex);
            if (!result.IsValid)
                return RecordResult(result);
            Wires.Add(wire);
            InvalidatePlan();
            return RecordResult(ValidationResult.Success());
        }

        public void RemoveGate(IGate gate)
        {
            if (gate == null || !Gates.Remove(gate))
                return;
            Wires.RemoveAll(w => w != null &&
                (ReferenceEquals(w.SourceGate, gate) || ReferenceEquals(w.TargetGate, gate)));
            InvalidatePlan();
            TrySimulate();
        }

        public void RemoveWire(Wire wire)
        {
            if (wire == null || !Wires.Remove(wire))
                return;
            InvalidatePlan();
            TrySimulate();
        }

        public void Clear()
        {
            foreach (var gate in Gates)
                if (gate?.Inputs != null)
                    Array.Clear(gate.Inputs);
            Wires.Clear();
            Gates.Clear();
            InvalidatePlan();
            HasFeedbackLoop = false;
            LastError = string.Empty;
        }

        public void ReplaceWith(CircuitManager source)
        {
            ArgumentNullException.ThrowIfNull(source);
            var result = source.TrySimulate();
            if (!result.IsValid)
                throw new InvalidOperationException(result.Message);
            if (ReferenceEquals(source, this))
                return;
            var gates = source.Gates.ToArray();
            var wires = source.Wires.ToArray();
            Clear();
            Gates.AddRange(gates);
            Wires.AddRange(wires);
        }

        public ValidationResult TryValidate()
        {
            if (PlanIsCurrent())
                return RecordResult(ValidationResult.Success());
            var result = CircuitValidator.TryGetEvaluationOrder(this, out var order);
            HasFeedbackLoop = result.HasCycle;
            if (!result.IsValid)
            {
                InvalidatePlan();
                return RecordResult(result);
            }
            evaluationOrder = order.ToArray();
            incomingWires = new Dictionary<IGate, List<Wire>>(Gates.Count, ReferenceEqualityComparer.Instance);
            foreach (var gate in Gates)
                incomingWires.Add(gate, new List<Wire>());
            foreach (var wire in Wires)
                incomingWires[wire.TargetGate].Add(wire);
            snapshotGates = Gates.ToArray();
            snapshotWires = Wires.ToArray();
            snapshotIds = new string[Gates.Count];
            snapshotTypes = new string[Gates.Count];
            snapshotInputs = new bool[Gates.Count][];
            for (int i = 0; i < Gates.Count; i++)
            {
                snapshotIds[i] = Gates[i].Id;
                snapshotTypes[i] = Gates[i].GateType;
                snapshotInputs[i] = Gates[i].Inputs;
            }
            return RecordResult(ValidationResult.Success());
        }

        public void Simulate()
        {
            var result = TrySimulate();
            if (!result.IsValid)
                throw new InvalidOperationException(result.Message);
        }

        public ValidationResult TrySimulate()
        {
            var result = TryValidate();
            if (!result.IsValid)
                return result;
            foreach (var gate in evaluationOrder!)
                Array.Clear(gate.Inputs);
            foreach (var gate in evaluationOrder!)
            {
                foreach (var wire in incomingWires[gate])
                    wire.TransferSignal();
                gate.Evaluate();
            }
            return RecordResult(ValidationResult.Success());
        }

        private bool PlanIsCurrent()
        {
            if (evaluationOrder == null || snapshotGates.Length != Gates.Count || snapshotWires.Length != Wires.Count)
                return false;
            for (int i = 0; i < Gates.Count; i++)
            {
                var gate = Gates[i];
                if (gate == null || !ReferenceEquals(gate, snapshotGates[i]) ||
                    !string.Equals(gate.Id, snapshotIds[i], StringComparison.Ordinal) ||
                    !string.Equals(gate.GateType, snapshotTypes[i], StringComparison.Ordinal) ||
                    !ReferenceEquals(gate.Inputs, snapshotInputs[i]))
                    return false;
            }
            for (int i = 0; i < Wires.Count; i++)
                if (!ReferenceEquals(Wires[i], snapshotWires[i]))
                    return false;
            return true;
        }

        private void InvalidatePlan()
        {
            evaluationOrder = null;
            incomingWires.Clear();
            snapshotGates = Array.Empty<IGate>();
            snapshotWires = Array.Empty<Wire>();
            snapshotIds = Array.Empty<string>();
            snapshotTypes = Array.Empty<string>();
            snapshotInputs = Array.Empty<bool[]>();
        }

        private ValidationResult RecordResult(ValidationResult result)
        {
            LastError = result.IsValid ? string.Empty : result.Message;
            return result;
        }
    }
}
