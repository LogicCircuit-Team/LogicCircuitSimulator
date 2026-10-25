using System.Collections.Generic;
using LogicCore.Management;

namespace LogicCore.Simulation
{
    public enum StepStage
    {
        TransferSignals,
        EvaluateGates
    }

    public class StepSimulator
    {
        private readonly CircuitManager _manager;
        public StepStage CurrentStage { get; private set; } = StepStage.TransferSignals;
        public int StepCount { get; private set; } = 0;
        public string LastActionDescription { get; private set; } = "جاهز لبدء المحاكاة التدريجية.";

        public StepSimulator(CircuitManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            CurrentStage = StepStage.TransferSignals;
            StepCount = 0;
            LastActionDescription = "تمت إعادة ضبط الوضع التعليمي.";
        }

        public void ExecuteNextStep()
        {
            StepCount++;

            if (CurrentStage == StepStage.TransferSignals)
            {
                foreach (var wire in _manager.Wires)
                {
                    wire.TransferSignal();
                }
                LastActionDescription = $"خطوة {StepCount}: تم نقل الإشارات المنطقية عبر جميع الأسلاك إلى مداخل البوابات.";
                CurrentStage = StepStage.EvaluateGates;
            }
            else
            {
                foreach (var gate in _manager.Gates)
                {
                    gate.Evaluate();
                }
                LastActionDescription = $"خطوة {StepCount}: تم تقييم المنطق لجميع البوابات وتحديث قيم المخارج.";
                CurrentStage = StepStage.TransferSignals;
            }
        }
    }
}