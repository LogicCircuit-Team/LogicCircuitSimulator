using LogicCore.Core;
using System.Linq;

namespace LogicCore.Gates
{
    public class NOR : Gate
    {
        public override string GateType => "NOR";

        public NOR(int inputCount = 2) : base(inputCount) { }

        public override void Evaluate()
        {
            Output = !(Inputs.Length > 0 && Inputs.Any(i => i));
        }
    }
}