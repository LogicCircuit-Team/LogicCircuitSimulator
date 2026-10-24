using LogicCore.Connections;
using LogicCore.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LogicCore.Management
{
    public class CircuitManager
    {
        public List<IGate> Gates { get; } = new List<IGate>();
        public List<Wire> Wires { get; } = new List<Wire>();

        public void AddGate(IGate gate)
        {
            Gates.Add(gate);
            Simulate();
        }

        public void RemoveGate(IGate gate)
        {
            var attached = Wires.Where(w => w.SourceGate == gate || w.TargetGate == gate).ToList();
            foreach (var w in attached)
            {
                RemoveWire(w);
            }
            Gates.Remove(gate);
            Simulate();
        }

        public void AddWire(Wire wire)
        {
            // اعتماد سلك واحد لكل مدخل مع السماح بتفرع المخرج
            var existing = Wires.FirstOrDefault(w => w.TargetGate == wire.TargetGate && w.TargetInputIndex == wire.TargetInputIndex);
            if (existing != null)
            {
                RemoveWire(existing);
            }

            Wires.Add(wire);
            Simulate();
        }

        public void RemoveWire(Wire wire)
        {
            Wires.Remove(wire);
            // إعادة المدخل غير الموصول إلى false طبقاً للوثيقة
            if (wire.TargetGate.Inputs != null && wire.TargetInputIndex < wire.TargetGate.Inputs.Length)
            {
                wire.TargetGate.Inputs[wire.TargetInputIndex] = false;
            }
            Simulate();
        }

        public void Clear()
        {
            Wires.Clear();
            Gates.Clear();
        }

        public void Simulate()
        {
            int maxIterations = Gates.Count + 2;
            for (int i = 0; i < maxIterations; i++)
            {
                foreach (var wire in Wires)
                {
                    wire.TransferSignal();
                }

                foreach (var gate in Gates)
                {
                    gate.Evaluate();
                }
            }
        }
    }
}