using LogicCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogicCore.Connections
{
    public class Wire
    {
        public IGate SourceGate { get; }
        public int SourcePinIndex { get; }
        public IGate TargetGate { get; }
        public int TargetInputIndex { get; } 

        public Wire(IGate sourceGate, IGate targetGate, int targetInputIndex)
        {
            SourceGate = sourceGate;
            SourcePinIndex = 0;
            TargetGate = targetGate;
            TargetInputIndex = targetInputIndex;
        }

        public void TransferSignal()
        {
            TargetGate.Inputs[TargetInputIndex] = SourceGate.Output;
        }
    }
}