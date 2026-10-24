using System;
using System.Collections.Generic;
using System.Text;

namespace LogicCore.Interfaces
{
    public interface IGate
    {
        string Id { get; set; }
        string GateType { get; }
        double X { get; set; }
        double Y { get; set; }
        bool[] Inputs { get; }
        bool Output { get; } 
        void Evaluate();
    }
}