using LogicCore.Management;
using System;
using System.Collections.Generic;
using System.Text.Json;
using LogicCore.Gates;
using LogicCore.Connections;
using LogicCore.Interfaces;
namespace LogicCore.Serialization

{   
    
    // CircuitManager ==>>>>>>> CircuitDto // Converts circuit data between CircuitManager
     // , CircuitDto, and JSON.
    public static class CircuitSerializer
    {

        public static CircuitDto ToDto(CircuitManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);
            var dto = new CircuitDto();
            foreach (var gate in manager.Gates)
            {
                dto.Gates.Add(new GateDto
                {
                    Id = gate.Id,
                    GateType = gate.GateType,
                    X = gate.X,
                    Y = gate.Y,
                    SwitchState = gate is Switch switchGate
                    ? switchGate.State : null


                });

            }

            foreach (var wire in manager.Wires)
            {
                dto.Wires.Add(new WireDto
                {
                    SourceGateId = wire.SourceGate.Id,
                    TargetGateId = wire.TargetGate.Id,
                    TargetInputIndex = wire.TargetInputIndex,


                });

            }
            return dto;

        }




        /// <summary>
        /// Sets how the JSON text will look.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        };


        /// <summary>
        /// Converts the circuit data to JSON text.
        /// </summary>

        public static string ToJson(CircuitManager manager)
        {
            var dto = ToDto(manager);
            return JsonSerializer.Serialize(dto, JsonOptions);

        }



        // Converts JSON text back into circuit data
        public static CircuitDto FromJson(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            var dto = JsonSerializer.Deserialize<CircuitDto>(json, JsonOptions);


            return dto ?? throw new JsonException("The circuit data is empty.");

        }

        // Converts JSON text directly into a CircuitManager.
        public static CircuitManager FromJsonToManager(string json)
        {
            var dto = FromJson(json);
            return FromDto(dto);
        }

        // Converts saved circuit data back into a CircuitManager.
        public static CircuitManager FromDto(CircuitDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);


            if (dto.SchemaVersion!=1) 
            {
                throw new JsonException("Unsupported circuit schema version.");
            }

            if (dto.Gates is null ||  dto.Wires is null)
            {
                throw new JsonException("Circuit gates or wires are missing.");
            }



            var manager = new CircuitManager();
            var gatesById = new Dictionary<string, IGate>(dto.Gates.Count, StringComparer.Ordinal);

            // Creates each gate and restores its saved properties.

            foreach (var gateDto in dto.Gates)
            {


                if(gateDto is null)
                    throw new JsonException("A gate entry is missing."); // Rejects incomplete data.

                if (string.IsNullOrWhiteSpace(gateDto.Id)) 
                    throw new JsonException("A gate ID is missing.");
                if (gatesById.ContainsKey(gateDto.Id))
                    throw new JsonException($"Duplicate gate ID: {gateDto.Id}");

                if (!double.IsFinite(gateDto.X) || !double.IsFinite(gateDto.Y)) 
                    throw new JsonException($"Invalid position for gate: {gateDto.Id}"); 
                // Rejects an invalid position.




                IGate gate = gateDto.GateType switch
                {
                    "AND" => new AndGate(),
                    "OR" => new ORGate(),
                    "NOT" => new NotGate(),
                    "NAND" => new NANDGate(),
                    "NOR" => new NOR(),
                    "XOR" => new XORGate(),
                    "SWITCH" => new Switch(),
                    "LAMP" => new Lamp(),
                    _ => throw new JsonException(
                        $"Unknown gate type: {gateDto.GateType}")
                };


                gate.Id = gateDto.Id;
                gate.X = gateDto.X;
                gate.Y = gateDto.Y;

                if (gate is Switch switchGate)

                {
                    switchGate.State = gateDto.SwitchState ?? false;
                }

                manager.Gates.Add(gate);
                gatesById.Add(gate.Id, gate);

            }





            // Recreates each wire using the restored gates
            foreach (var wireDto in dto.Wires)
            {

                if (wireDto is null) // Checks that the wire entry exists.
                    throw new JsonException("A wire entry is missing.");

                if (string.IsNullOrWhiteSpace(wireDto.SourceGateId) ||
                    string.IsNullOrWhiteSpace(wireDto.TargetGateId)) // Checks both gate IDs.
                    throw new JsonException("A wire source or target ID is missing.");

                if (!gatesById.TryGetValue(wireDto.SourceGateId, out var sourceGate)) // Checks the source gate.
                    throw new JsonException($"Source gate was not found: {wireDto.SourceGateId}");

                if (!gatesById.TryGetValue(wireDto.TargetGateId, out var targetGate)) // Checks the target gate.
                    throw new JsonException($"Target gate was not found: {wireDto.TargetGateId}");

                if (wireDto.TargetInputIndex < 0 ||
                    wireDto.TargetInputIndex >= targetGate.Inputs.Length) // Checks the target input.
                    throw new JsonException("The target input index is invalid.");

                manager.Wires.Add(new Wire(sourceGate, targetGate, wireDto.TargetInputIndex));

             
            }

            var validation = manager.TrySimulate();
            if (!validation.IsValid)
                throw new JsonException(validation.Message);
            return manager;

        }





    }



}
