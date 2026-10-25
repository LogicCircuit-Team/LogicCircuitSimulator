using LogicCore.Gates;
using LogicCore.Management;

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
            ArgumentNullException.ThrowIfNull(circuitManager);
            var switchs = circuitManager.Gates.OfType<Switch>().OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            var lamps = circuitManager.Gates.OfType<Lamp>().OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            if (switchs.Count > 8)
                throw new InvalidOperationException("Cannot generate a truth table for more than 8 switches.");
            CircuitValidator.Validate(circuitManager);
            InputIds = GetInputIds(switchs);
            OutputIds = GetOutputIds(lamps);
            int totalRows = 1 << switchs.Count;
            var rows = new List<TruthTableRow>(totalRows);
            var originalStates = switchs.Select(x => x.State).ToArray();
            try
            {
                for (int rowIndex = 0; rowIndex < totalRows; rowIndex++)
                {
                    var inputs = new bool[switchs.Count];
                    for (int inputIndex = 0; inputIndex < switchs.Count; inputIndex++)
                    {
                        bool state = (rowIndex & (1 << (switchs.Count - inputIndex - 1))) != 0;
                        inputs[inputIndex] = state;
                        switchs[inputIndex].State = state;
                    }
                    circuitManager.Simulate();
                    var outputs = new bool[lamps.Count];
                    for (int outputIndex = 0; outputIndex < lamps.Count; outputIndex++)
                        outputs[outputIndex] = lamps[outputIndex].Output;
                    rows.Add(new TruthTableRow { InputValues = inputs, OutputValues = outputs });
                }
            }
            finally
            {
                for (int i = 0; i < switchs.Count; i++)
                    switchs[i].State = originalStates[i];
                circuitManager.Simulate();
            }
            return rows;
        }
    }
}
