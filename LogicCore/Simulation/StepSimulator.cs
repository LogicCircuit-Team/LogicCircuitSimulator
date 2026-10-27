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
        public string LastActionDescription { get; private set; } = "Ready for step simulation.";

        public StepSimulator(CircuitManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            CurrentStage = StepStage.TransferSignals;
            StepCount = 0;
            LastActionDescription = "Step simulation reset.";
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
                LastActionDescription = $"Step {StepCount}: transferred all wire signals to gate inputs.";
                CurrentStage = StepStage.EvaluateGates;
            }
            else
            {
                foreach (var gate in _manager.Gates)
                {
                    gate.Evaluate();
                }
                LastActionDescription = $"Step {StepCount}: evaluated all gates and updated their outputs.";
                CurrentStage = StepStage.TransferSignals;
            }
        }
    }
}