using LogicCore.Core;
using System.Linq;

namespace LogicCore.Gates
{
    public class NANDGate : Gate
    {
        public override string GateType => "NAND";

        public NANDGate(int inputCount = 2) : base(inputCount) { }

        public override void Evaluate()
        {
            Output = !(Inputs.Length > 0 && Inputs.All(i => i));
        }
    }
}