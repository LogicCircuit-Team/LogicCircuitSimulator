using LogicCore.Connections;
using LogicCore.Core;
using LogicCore.Gates;
using LogicCore.Interfaces;
using LogicCore.Management;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LogicCore.Analysis
{
    public class YosysNetlistAdapter
    {
        public CircuitManager ImportFromJson(string netlistJson, string topModule)
        {
            if (string.IsNullOrWhiteSpace(netlistJson))
            {
                throw new ArgumentException("We need Netlist JSON", nameof(netlistJson));
            }
            if (string.IsNullOrWhiteSpace(topModule))
            {
                throw new ArgumentException("We need top module name", nameof(topModule));
            }

            using var document = JsonDocument.Parse(netlistJson);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Netlist JSON root must be an object.");
            }

            bool hasModules = root.TryGetProperty("modules", out JsonElement modulesElement);
            if (hasModules == false)
            {
                throw new InvalidOperationException("Netlist JSON does not contain 'modules'.");
            }

            if (modulesElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("'modules' must be a JSON object.");
            }

            bool hasTopModule = modulesElement.TryGetProperty(topModule, out JsonElement topmodulesElement);
            if (hasTopModule == false)
            {
                throw new InvalidOperationException($"Top module '{topModule}' was not found in the netlist.");
            }

            if (topmodulesElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Top module must be a JSON object.");
            }

            if (topmodulesElement.TryGetProperty("memories", out JsonElement memoriesElement))
            {
                if (memoriesElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException("'memories' must be a JSON object.");
                }

                foreach (var memoryProperty in memoriesElement.EnumerateObject())
                {
                    throw new InvalidOperationException($"Memory '{memoryProperty.Name}' is not supported. Only combinational logic is allowed.");
                }
            }

            bool hasPorts = topmodulesElement.TryGetProperty("ports", out JsonElement portsElement);
            if (hasPorts == false)
            {
                throw new InvalidOperationException("Top module does not contain 'ports'.");
            }
            if (portsElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("'ports' must be a JSON object.");
            }

            bool hasCells = topmodulesElement.TryGetProperty("cells", out JsonElement cellsElement);
            if (hasCells == false)
            {
                throw new InvalidOperationException("Top module does not contain 'cells'.");
            }
            if (cellsElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("'cells' must be a JSON object.");
            }

            // انشاء متغيرات
            CircuitManager temporaryManager = new CircuitManager();
            Dictionary<int, IGate> signalSources = new Dictionary<int, IGate>();
            HashSet<string> usedPortNames = new HashSet<string>();
            Dictionary<Lamp, int> outputBits = new Dictionary<Lamp, int>();
            int inputIndex = 0;
            int outputIndex = 0;

            foreach (var portProperty in portsElement.EnumerateObject())
            {
                string portName = portProperty.Name;
                JsonElement portElement = portProperty.Value;

                if (portElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException($"Port '{portName}' must be a JSON object.");
                }

                if (usedPortNames.Contains(portName))
                {
                    throw new InvalidOperationException($"Port name '{portName}' is duplicated.");
                }
                usedPortNames.Add(portName);

                if (!portElement.TryGetProperty("direction", out JsonElement directionElement))
                {
                    throw new InvalidOperationException($"Port '{portName}' is missing 'direction' property.");
                }

                if (directionElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException($"The 'direction' of port '{portName}' must be a JSON string.");
                }

                string? direction = directionElement.GetString();

                if (direction != "input" && direction != "output")
                {
                    throw new InvalidOperationException($"Port '{portName}' has an invalid direction '{direction}'. Only 'input' or 'output' are supported.");
                }

                if (!portElement.TryGetProperty("bits", out JsonElement bitsElement))
                {
                    throw new InvalidOperationException($"Port '{portName}' is missing 'bits' property.");
                }

                int bitId = ReadSingleBitId(bitsElement, portName);

                if (direction == "input")
                {
                    Switch switchItem = new Switch
                    {
                        Id = "input_" + portName,
                        X = 100,
                        Y = 80 + inputIndex * 100
                    };

                    if (signalSources.ContainsKey(bitId))
                    {
                        throw new InvalidOperationException($"Signal bit '{bitId}' already has a source. Multiple sources are not allowed.");
                    }

                    temporaryManager.Gates.Add(switchItem);
                    signalSources.Add(bitId, switchItem);
                    inputIndex++;
                }
                else if (direction == "output")
                {
                    Lamp lampItem = new Lamp
                    {
                        Id = "output_" + portName,
                        X = 700,
                        Y = 80 + outputIndex * 100
                    };

                    temporaryManager.Gates.Add(lampItem);
                    outputBits.Add(lampItem, bitId);
                    outputIndex++;
                }
            }

            Dictionary<string, IGate> cellGates = new Dictionary<string, IGate>();
            int gateIndex = 0;

            foreach (var cellProperty in cellsElement.EnumerateObject())
            {
                string cellName = cellProperty.Name;
                JsonElement cellElement = cellProperty.Value;

                if (cellElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException($"Cell '{cellName}' must be a JSON object.");
                }

                if (cellGates.ContainsKey(cellName))
                {
                    throw new InvalidOperationException($"Cell name '{cellName}' is duplicated.");
                }

                if (!cellElement.TryGetProperty("type", out JsonElement typeElement) || typeElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException($"Cell '{cellName}' is missing valid 'type' property.");
                }

                string? cellType = typeElement.GetString();

                if (string.IsNullOrWhiteSpace(cellType))
                {
                    throw new InvalidOperationException($"The 'type' of cell '{cellName}' cannot be empty.");
                }

                IGate gate = CreateGate(cellType);
                gate.Id = "gate_" + cellName;
                gate.X = 350;
                gate.Y = 80 + gateIndex * 100;

                if (!cellElement.TryGetProperty("connections", out JsonElement connectionsElement) || connectionsElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException($"Cell '{cellName}' has missing or invalid 'connections' property.");
                }

                ValidateCellConnections(cellName, gate, connectionsElement);

                if (!connectionsElement.TryGetProperty("Y", out JsonElement outputBitsElement))
                {
                    throw new InvalidOperationException($"Cell '{cellName}' does not contain output connection 'Y'.");
                }

                int outputBitId = ReadSingleBitId(outputBitsElement, $"{cellName}.Y");

                if (signalSources.ContainsKey(outputBitId))
                {
                    throw new InvalidOperationException($"Signal bit '{outputBitId}' from cell '{cellName}' already has a source.");
                }

                signalSources.Add(outputBitId, gate);
                temporaryManager.Gates.Add(gate);
                cellGates.Add(cellName, gate);
                gateIndex++;
            }

            foreach (var cellProperty in cellsElement.EnumerateObject())
            {
                string cellName = cellProperty.Name;
                JsonElement cellElement = cellProperty.Value;
                IGate targetGate = cellGates[cellName];

                JsonElement connectionsElement = cellElement.GetProperty("connections");

                for (int pinIndex = 0; pinIndex < targetGate.Inputs.Length; pinIndex++)
                {
                    string pinName = pinIndex == 0 ? "A" : "B";

                    if (!connectionsElement.TryGetProperty(pinName, out JsonElement inputBitsElement))
                    {
                        throw new InvalidOperationException($"Cell '{cellName}' is missing connection for input '{pinName}'.");
                    }

                    int inputBitId = ReadSingleBitId(inputBitsElement, $"{cellName}.{pinName}");

                    if (!signalSources.TryGetValue(inputBitId, out IGate? sourceGate))
                    {
                        throw new InvalidOperationException($"Signal bit '{inputBitId}' for cell '{cellName}' input '{pinName}' has no defined source.");
                    }

                    Wire wire = new Wire(sourceGate, targetGate, pinIndex);
                    temporaryManager.Wires.Add(wire);
                }
            }

            foreach (var kvp in outputBits)
            {
                Lamp lamp = kvp.Key;
                int requiredBitId = kvp.Value;

                if (!signalSources.TryGetValue(requiredBitId, out IGate? sourceGate))
                {
                    throw new InvalidOperationException($"Signal bit '{requiredBitId}' for output '{lamp.Id}' has no defined source.");
                }

                Wire wire = new Wire(sourceGate, lamp, 0);
                temporaryManager.Wires.Add(wire);
            }

            ValidateNoCycles(temporaryManager);
            temporaryManager.Simulate();

            return temporaryManager;
        }

        private void ValidateNoCycles(CircuitManager manager)
        {
            var result = manager.TryValidate();
            if (!result.IsValid)
                throw new InvalidOperationException(result.Message);
        }

        private void ValidateCellConnections(string cellName, IGate gate, JsonElement connectionsElement)
        {
            HashSet<string> expectedPinNames = gate.Inputs.Length switch
            {
                1 => new HashSet<string> { "A", "Y" },
                2 => new HashSet<string> { "A", "B", "Y" },
                _ => throw new InvalidOperationException($"Unsupported number of inputs ({gate.Inputs.Length}) for gate in cell '{cellName}'.")
            };

            HashSet<string> seenPinNames = new HashSet<string>();

            foreach (var connectionProperty in connectionsElement.EnumerateObject())
            {
                string pinName = connectionProperty.Name;

                if (!expectedPinNames.Contains(pinName))
                {
                    throw new InvalidOperationException($"Cell '{cellName}' contains an unexpected pin connection '{pinName}'.");
                }

                if (seenPinNames.Contains(pinName))
                {
                    throw new InvalidOperationException($"Cell '{cellName}' contains a duplicate pin connection '{pinName}'.");
                }

                seenPinNames.Add(pinName);
            }

            foreach (var expectedPin in expectedPinNames)
            {
                if (!seenPinNames.Contains(expectedPin))
                {
                    throw new InvalidOperationException($"Cell '{cellName}' is missing required pin connection '{expectedPin}'.");
                }
            }
        }

        private IGate CreateGate(string cellType)
        {
            switch (cellType)
            {
                case "$_AND_": return new AndGate();
                case "$_OR_": return new ORGate();
                case "$_NOT_": return new NotGate();
                case "$_XOR_": return new XORGate();
                case "$_NAND_": return new NANDGate();
                case "$_NOR_": return new NOR();
                default:
                    throw new InvalidOperationException($"Unsupported cell type '{cellType}'");
            }
        }

        private int ReadSingleBitId(JsonElement bitsElement, string context)
        {
            if (bitsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"[{context}] bitsElement must be a JSON array.");
            }
            if (bitsElement.GetArrayLength() != 1)
            {
                throw new InvalidOperationException($"[{context}] The array must contain exactly one element.");
            }

            JsonElement bitElement = bitsElement[0];

            if (bitElement.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException($"[{context}] The bit element must be a JSON number.");
            }
            if (!bitElement.TryGetInt32(out int bitId) || bitId < 0)
            {
                throw new InvalidOperationException($"[{context}] Failed to read a valid non-negative bit ID.");
            }

            return bitId;
        }
    }
}