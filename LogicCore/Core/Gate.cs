using System;
using LogicCore.Interfaces;

namespace LogicCore.Core
{
    public abstract class Gate : IGate
    {

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public abstract string GateType { get; }
        public double X { get; set; } = 150;
        public double Y { get; set; } = 150;
        public bool[] Inputs { get; protected set; }
        public bool Output { get; protected set; }

        protected Gate(int inputCount = 2)
        {
            Inputs = new bool[Math.Max(0, inputCount)];
        }

        public abstract void Evaluate();
    }
}