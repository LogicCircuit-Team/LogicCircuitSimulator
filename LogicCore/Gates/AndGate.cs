// LogicCore/Gates/AndGate.cs
using LogicCore.Core;
using System.Linq;

namespace LogicCore.Gates
{
    public class AndGate : Gate
    {
        public override string GateType => "AND";

        // إمكانية تمرير عدد المدخلات المطلوب (افتراضياً 2)
        public AndGate(int inputCount = 2) : base(inputCount)
        {
        }

        public override void Evaluate()
        {
            // تكون النتيجة true فقط إذا كانت جميع المداخل true
            Output = Inputs.Length > 0 && Inputs.All(i => i);
        }
    }
}