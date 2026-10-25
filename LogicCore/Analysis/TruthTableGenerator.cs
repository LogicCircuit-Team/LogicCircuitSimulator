using LogicCore.Gates;
using LogicCore.Management;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LogicCore.Analysis
{

    public class TruthTableGenerator
    {
        public string[] InputIds { get; private set; } = Array.Empty<string>();
        public string[] OutputIds { get; private set; } = Array.Empty<string>();

        public string[] GetInputIds(List<Switch> switchs)
        {
            return switchs.Select(x => x.Id).ToArray();
        }

        public string[] GetOutputIds(List<Lamp> lamps)
        {
            return lamps.Select(x => x.Id).ToArray();
        }
        public List<TruthTableRow> Generate(CircuitManager circuitManager)
        {
            List<TruthTableRow> rows = new List<TruthTableRow>();
            List<Switch> switchs = new List<Switch>();
            List<Lamp> Lamps = new List<Lamp>();
            foreach (var item in circuitManager.Gates)
            {
                if (item is Switch switchItem)
                {
                    switchs.Add(switchItem);
                }
                else if (item is Lamp lampItem)
                {
                    Lamps.Add(lampItem);

                }
            }
            if (switchs.Count > 8)
            {

                throw new InvalidOperationException("Cannot generate a truth table for more than 8 switches.");
            }
            else
            {
                CircuitValidator.Validate(circuitManager);
                switchs = switchs.OrderBy(x => x.Id).ToList();
                Lamps = Lamps.OrderBy(x => x.Id).ToList();
                InputIds = GetInputIds(switchs);
                OutputIds = GetOutputIds(Lamps);
                bool[] originalStates = switchs.Select(x => x.State).ToArray();
                try
                {
                    int totalRows = Convert.ToInt32(Math.Pow(2, switchs.Count));
                    for (int rowIndex = 0; rowIndex < totalRows; rowIndex++)
                    {
                        bool[] inputValues = new bool[switchs.Count];
                        string binaryInputs = Convert.ToString(rowIndex, 2).PadLeft(switchs.Count, '0');

                        for (int inputIndex = 0; inputIndex < switchs.Count; inputIndex++)
                        {
                            bool inputState = binaryInputs[inputIndex] == '1';
                            inputValues[inputIndex] = inputState;

                            switchs[inputIndex].State = inputState;
                        }
                        circuitManager.Simulate();
                        bool[] outputValues = Lamps.Select(lamp => lamp.Output).ToArray();

                        TruthTableRow row = new TruthTableRow
                        {
                            InputValues = inputValues,
                            OutputValues = outputValues
                        };

                        rows.Add(row);
                    }
                }
                finally
                {
                    for (int i = 0; i < switchs.Count; i++)
                    {
                        switchs[i].State = originalStates[i];
                    }

                    //دالة Simulate موجودة في ملف السيركيت منجر الي بتقوم في حساب الاسلاك والاويرات فبدي اعملها رست 
                    circuitManager.Simulate();
                }
                return rows;

            }
        }

    }
}