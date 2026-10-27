using LogicCore.Core;
using System.Linq;

namespace LogicCore.Gates
{
    public class XORGate : Gate
    {
        public override string GateType => "XOR";

        public XORGate(int inputCount = 2) : base(inputCount) { }

        public override void Evaluate()
        {

            Output = Inputs.Count(i => i) % 2 != 0;
        }
    }
}