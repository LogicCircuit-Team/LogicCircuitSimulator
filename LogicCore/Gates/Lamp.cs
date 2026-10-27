using LogicCore.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogicCore.Gates
{
    public class Lamp : Gate
    {
        public override string GateType => "LAMP";

        public Lamp() : base(1)
        {
        }

        public override void Evaluate()
        {
            Output = Inputs[0];
        }
    }
}