using System;
using System.Collections.Generic;
using LogicCore.Connections;
using LogicCore.Interfaces;
using LogicCore.Management;

namespace LogicCore.Validation
{
    public static class CircuitValidator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public bool HasCycle { get; set; }

            public static ValidationResult Success() => new() { IsValid = true };

            public static ValidationResult Fail(string message, bool hasCycle = false) =>
                new() { IsValid = false, Message = message, HasCycle = hasCycle };
        }

        public static ValidationResult CanAddGate(CircuitManager? manager, IGate? gate)
        {
            if (manager == null)
                return ValidationResult.Fail("The circuit manager is missing.");

            var result = ValidateGate(gate);
            if (!result.IsValid)
                return result;

            foreach (var existing in manager.Gates)
            {
                if (existing != null &&
                    string.Equals(existing.Id, gate!.Id, StringComparison.Ordinal))
                {
                    return ValidationResult.Fail($"Gate ID '{gate.Id}' is duplicated.");
                }
            }

            return ValidationResult.Success();
        }

        public static ValidationResult CanConnect(
            CircuitManager? manager, IGate? source, IGate? target, int targetInputIndex)
        {
            if (source == null || target == null)
                return ValidationResult.Fail("A connection endpoint is invalid.");

            return ValidateAndOrder(
                manager, new Wire(source, target, targetInputIndex), out _);
        }

        public static ValidationResult TryGetEvaluationOrder(
            CircuitManager? manager, out List<IGate> order)
        {
            return ValidateAndOrder(manager, null, out order);
        }

        private static ValidationResult ValidateGate(IGate? gate)
        {
            if (gate == null)
                return ValidationResult.Fail("A gate is required.");

            if (string.IsNullOrWhiteSpace(gate.Id))
                return ValidationResult.Fail("A gate ID is required.");

            if (gate.Inputs == null)
                return ValidationResult.Fail($"Inputs on gate '{gate.Id}' are not initialized.");

            return ValidationResult.Success();
        }

        private static ValidationResult ValidateAndOrder(
            CircuitManager? manager, Wire? proposedWire, out List<IGate> order)
        {
            order = new List<IGate>();

            if (manager == null)
                return ValidationResult.Fail("The circuit manager is missing.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var incomingCounts = new Dictionary<IGate, int>(ReferenceEqualityComparer.Instance);
            var outgoing = new Dictionary<IGate, List<IGate>>(ReferenceEqualityComparer.Instance);
            var occupiedInputs = new Dictionary<IGate, HashSet<int>>(ReferenceEqualityComparer.Instance);

            foreach (var gate in manager.Gates)
            {
                var result = ValidateGate(gate);
                if (!result.IsValid)
                    return result;

                if (!ids.Add(gate.Id))
                    return ValidationResult.Fail($"Gate ID '{gate.Id}' is duplicated.");

                incomingCounts.Add(gate, 0);
                outgoing.Add(gate, new List<IGate>());
                occupiedInputs.Add(gate, new HashSet<int>());
            }

            ValidationResult AddEdge(Wire? wire)
            {
                if (wire == null || wire.SourceGate == null || wire.TargetGate == null)
                    return ValidationResult.Fail("A wire or one of its endpoints is invalid.");

                var source = wire.SourceGate;
                var target = wire.TargetGate;

                if (!incomingCounts.ContainsKey(source) || !incomingCounts.ContainsKey(target))
                    return ValidationResult.Fail("Both wire endpoints must belong to this circuit.");

                if (ReferenceEquals(source, target))
                    return ValidationResult.Fail("A gate cannot connect to itself. Feedback is not supported.", true);

                if (string.Equals(source.GateType, "LAMP", StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.Fail("A lamp cannot be used as a signal source.");

                if (target.Inputs.Length == 0 ||
                    string.Equals(target.GateType, "SWITCH", StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Fail("The target gate has no input pins.");
                }

                int index = wire.TargetInputIndex;
                if (index < 0 || index >= target.Inputs.Length)
                    return ValidationResult.Fail($"Input index {index} is outside the target gate's input range.");

                if (!occupiedInputs[target].Add(index))
                {
                    return ValidationResult.Fail(
                        $"Input {index + 1} already has a wire. Only one wire per input is allowed.");
                }

                outgoing[source].Add(target);
                incomingCounts[target]++;
                return ValidationResult.Success();
            }

            foreach (var wire in manager.Wires)
            {
                var result = AddEdge(wire);
                if (!result.IsValid)
                    return result;
            }

            if (proposedWire != null)
            {
                var result = AddEdge(proposedWire);
                if (!result.IsValid)
                    return result;
            }

            var ready = new Queue<IGate>();
            foreach (var gate in manager.Gates)
            {
                if (incomingCounts[gate] == 0)
                    ready.Enqueue(gate);
            }

            while (ready.Count > 0)
            {
                var gate = ready.Dequeue();
                order.Add(gate);

                foreach (var target in outgoing[gate])
                {
                    incomingCounts[target]--;
                    if (incomingCounts[target] == 0)
                        ready.Enqueue(target);
                }
            }

            if (order.Count != manager.Gates.Count)
            {
                order.Clear();
                return ValidationResult.Fail(
                    "The circuit contains a cycle. Only combinational circuits without feedback are supported.", true);
            }

            return ValidationResult.Success();
        }
    }
}
