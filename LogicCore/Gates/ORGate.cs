using LogicCore.Core;
using System.Linq;

namespace LogicCore.Gates
{
    public class ORGate : Gate
    {
        public override string GateType => "OR";

        public ORGate(int inputCount = 2) : base(inputCount) { }

        public override void Evaluate()
        {
            Output = Inputs.Length > 0 && Inputs.Any(i => i);
        }
    }
}