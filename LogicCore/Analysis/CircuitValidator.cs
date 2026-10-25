using LogicCore.Interfaces;
using LogicCore.Management;
using System;
using System.Collections.Generic;

namespace LogicCore.Analysis
{
    internal static class CircuitValidator
    {
        internal static void Validate(CircuitManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);

            Dictionary<IGate, int> incomingWireCounts = new Dictionary<IGate, int>();
            Dictionary<IGate, List<IGate>> outgoingGates = new Dictionary<IGate, List<IGate>>();
            Dictionary<IGate, bool[]> connectedInputs = new Dictionary<IGate, bool[]>();
            HashSet<string> usedGateIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var gate in manager.Gates)
            {
                if (string.IsNullOrWhiteSpace(gate.Id))
                {
                    throw new InvalidOperationException("Every gate must have an ID.");
                }

                if (!usedGateIds.Add(gate.Id))
                {
                    throw new InvalidOperationException($"Gate ID '{gate.Id}' is duplicated.");
                }

                incomingWireCounts.Add(gate, 0);
                outgoingGates.Add(gate, new List<IGate>());
                connectedInputs.Add(gate, new bool[gate.Inputs.Length]);
            }

            foreach (var wire in manager.Wires)
            {
                if (!incomingWireCounts.ContainsKey(wire.SourceGate) ||
                    !incomingWireCounts.ContainsKey(wire.TargetGate))
                {
                    throw new InvalidOperationException("A wire references a gate that is not part of the circuit.");
                }

                int inputIndex = wire.TargetInputIndex;
                if (inputIndex < 0 || inputIndex >= wire.TargetGate.Inputs.Length)
                {
                    throw new InvalidOperationException($"Input {inputIndex} on gate '{wire.TargetGate.Id}' does not exist.");
                }

                if (connectedInputs[wire.TargetGate][inputIndex])
                {
                    throw new InvalidOperationException($"More than one wire connected to input {inputIndex} on gate '{wire.TargetGate.Id}'.");
                }

                connectedInputs[wire.TargetGate][inputIndex] = true;
                incomingWireCounts[wire.TargetGate]++;
                outgoingGates[wire.SourceGate].Add(wire.TargetGate);
            }

            foreach (var gate in manager.Gates)
            {
                for (int inputIndex = 0; inputIndex < gate.Inputs.Length; inputIndex++)
                {
                    if (!connectedInputs[gate][inputIndex])
                    {
                        throw new InvalidOperationException($"Input {inputIndex} on gate '{gate.Id}' is not connected.");
                    }
                }
            }

            Queue<IGate> readyGates = new Queue<IGate>();
            foreach (var entry in incomingWireCounts)
            {
                if (entry.Value == 0)
                {
                    readyGates.Enqueue(entry.Key);
                }
            }

            int processedGateCount = 0;
            while (readyGates.Count > 0)
            {
                IGate currentGate = readyGates.Dequeue();
                processedGateCount++;

                foreach (var targetGate in outgoingGates[currentGate])
                {
                    incomingWireCounts[targetGate]--;
                    if (incomingWireCounts[targetGate] == 0)
                    {
                        readyGates.Enqueue(targetGate);
                    }
                }
            }

            if (processedGateCount != manager.Gates.Count)
            {
                throw new InvalidOperationException("The circuit contains a cycle. Feedback connections are not supported.");
            }
        }
    }
}
