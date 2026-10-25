using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Text;

namespace LogicCore.Serialization
{
   public sealed class CircuitDto
    {
        ///SchemaVersion
        [JsonRequired]
         public int SchemaVersion { get; set; } = 1; 
        [JsonRequired]
         public List<GateDto> Gates { get; set; }= new();
        [JsonRequired]
         public List<WireDto> Wires { get; set; }= new();

    }

 

        public sealed class WireDto
      {
           public string SourceGateId { get; set; } = string.Empty;
           public string TargetGateId { get; set; } = string.Empty;
           public int TargetInputIndex { get; set; }
       }
   


    public sealed class GateDto
    {
        public string Id { get; set; } = string.Empty;
        public string GateType { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public bool? SwitchState { get; set; }
    }

}
