using LogicCore.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogicCore.Gates
{
    public class NotGate : Gate
    {
        public override string GateType => "NOT";

        public NotGate() : base(1)
        {
        }

        public override void Evaluate()
        {
            Output = !Inputs[0];
        }
    }
}