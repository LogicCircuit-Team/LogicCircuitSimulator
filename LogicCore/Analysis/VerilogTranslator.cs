using System;
using System.Collections.Generic;
using System.Text;
using LogicCore.Gates;
using System.Linq;
using LogicCore.Management;
using LogicCore.Interfaces;

namespace LogicCore.Analysis
{
    public class VerilogTranslator
    {
        private HashSet<string> reservedKeywords = new HashSet<string>
        {
            "accept_on", "alias", "always", "always_comb", "always_ff",
"always_latch", "and", "assert", "assign", "assume",
"automatic", "before", "begin", "bind", "bins",
"binsof", "bit", "break", "buf", "bufif0",
"bufif1", "byte", "case", "casex", "casez",
"cell", "chandle", "checker", "class", "clocking",
"cmos", "config", "const", "constraint", "context",
"continue", "cover", "covergroup", "coverpoint", "cross",
"deassign", "default", "defparam", "design", "disable",
"dist", "do", "edge", "else", "end",
"endcase", "endchecker", "endclass", "endclocking", "endconfig",
"endfunction", "endgenerate", "endgroup", "endinterface", "endmodule",
"endpackage", "endprimitive", "endprogram", "endproperty", "endspecify",
"endsequence", "endtable", "endtask", "enum", "event",
"eventually", "expect", "export", "extends", "extern",
"final", "first_match", "for", "force", "foreach",
"forever", "fork", "forkjoin", "function", "generate",
"genvar", "global", "highz0", "highz1", "if",
"iff", "ifnone", "ignore_bins", "illegal_bins", "implements",
"implies", "import", "incdir", "include", "initial",
"inout", "input", "inside", "instance", "int",
"integer", "interconnect", "interface", "intersect", "join",
"join_any", "join_none", "large", "let", "liblist",
"library", "local", "localparam", "logic", "longint",
"macromodule", "matches", "medium", "modport", "module",
"nand", "negedge", "nettype", "new", "nexttime",
"nmos", "nor", "noshowcancelled", "not", "notif0",
"notif1", "null", "or", "output", "package",
"packed", "parameter", "pmos", "posedge", "primitive",
"priority", "program", "property", "protected", "pull0",
"pull1", "pulldown", "pullup", "pulsestyle_ondetect", "pulsestyle_onevent",
"pure", "rand", "randc", "randcase", "randsequence",
"rcmos", "real", "realtime", "ref", "reg",
"reject_on", "release", "repeat", "restrict", "return",
"rnmos", "rpmos", "rtran", "rtranif0", "rtranif1",
"s_always", "s_eventually", "s_nexttime", "s_until", "s_until_with",
"scalared", "sequence", "shortint", "shortreal", "showcancelled",
"signed", "small", "soft", "solve", "specify",
"specparam", "static", "string", "strong", "strong0",
"strong1", "struct", "super", "supply0", "supply1",
"sync_accept_on", "sync_reject_on", "table", "tagged", "task",
"this", "throughout", "time", "timeprecision", "timeunit",
"tran", "tranif0", "tranif1", "tri", "tri0",
"tri1", "triand", "trior", "trireg", "type",
"typedef", "union", "unique", "unique0", "unsigned",
"until", "until_with", "untyped", "use", "uwire",
"var", "vectored", "virtual", "void", "wait",
"wait_order", "wand", "weak", "weak0", "weak1",
"while", "wildcard", "wire", "with", "within",
"wor", "xnor", "xor"
        };
        public string ExportToVerilog(CircuitManager manager, string moduleName, bool useSystemVerilog)
        {
            List<Switch> switchs = new List<Switch>();
            List<Lamp> Lamps = new List<Lamp>();
            List<IGate> logicGates = new List<IGate>();
            List<string> portNames = new List<string>();
            Dictionary<IGate, string> signalNames = new Dictionary<IGate, string>();
            string signalType = string.Empty;


            ValidateModuleName(moduleName);
            CircuitValidator.Validate(manager);

            foreach (var item in manager.Gates)
            {
                if (item is Switch switchItem)
                {
                    switchs.Add(switchItem);
                }
                else if (item is Lamp lampItem)
                {
                    Lamps.Add(lampItem);
                }
                else
                {
                    logicGates.Add(item);
                }
            }

            switchs = switchs.OrderBy(x => x.Id).ToList();
            Lamps = Lamps.OrderBy(x => x.Id).ToList();
            logicGates = logicGates.OrderBy(x => x.Id).ToList();

            for (int inputIndex = 0; inputIndex < switchs.Count; inputIndex++)
            {
                signalNames.Add(switchs[inputIndex], "in_" + inputIndex);
            }

            for (int outputIndex = 0; outputIndex < Lamps.Count; outputIndex++)
            {
                signalNames.Add(Lamps[outputIndex], "out_" + outputIndex);
            }

            for (int gateIndex = 0; gateIndex < logicGates.Count; gateIndex++)
            {
                signalNames.Add(logicGates[gateIndex], "n_" + gateIndex);
            }

            foreach (var switchItem in switchs)
            {
                portNames.Add(signalNames[switchItem]);
            }

            foreach (var lampItem in Lamps)
            {
                portNames.Add(signalNames[lampItem]);
            }

            string portsText = string.Join(", ", portNames);

            if (useSystemVerilog)
            {
                signalType = "logic";
            }
            else
            {
                signalType = "wire";
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine("module " + moduleName + "(" + portsText + ");");

            foreach (var switchItem in switchs)
            {
                builder.AppendLine("input " + signalType + " " + signalNames[switchItem] + ";");
            }

            foreach (var lampItem in Lamps)
            {
                builder.AppendLine(" output " + signalType + " " + signalNames[lampItem] + ";");
            }

            foreach (var gateItem in logicGates)
            {
                builder.AppendLine(signalType + " " + signalNames[gateItem] + ";");
            }

            foreach (var gateItem in logicGates)
            {
                List<string> inputSignals = new List<string>();
                for (int i = 0; i < gateItem.Inputs.Length; i++)
                {
                    inputSignals.Add(GetInputSignalName(manager, gateItem, i, signalNames));
                }

                string expression = string.Empty;

                switch (gateItem.GateType)
                {
                    case "AND":
                        if (inputSignals.Count != 2)
                            throw new InvalidOperationException("AND gate requires exactly 2 inputs.");
                        expression = inputSignals[0] + " & " + inputSignals[1];
                        break;
                    case "OR":
                        if (inputSignals.Count != 2)
                            throw new InvalidOperationException("OR gate requires exactly 2 inputs.");
                        expression = inputSignals[0] + " | " + inputSignals[1];
                        break;
                    case "NOT":
                        if (inputSignals.Count != 1)
                            throw new InvalidOperationException("NOT gate requires exactly 1 inputs.");
                        expression = " ~ " + inputSignals[0];
                        break;
                    case "NAND":
                        if (inputSignals.Count != 2)
                            throw new InvalidOperationException("NAND gate requires exactly 2 inputs.");
                        expression = "~(" + inputSignals[0] + "  & " + inputSignals[1] + ")";
                        break;
                    case "NOR":
                        if (inputSignals.Count != 2)
                            throw new InvalidOperationException("NOR gate requires exactly 2 inputs.");
                        expression = "~(" + inputSignals[0] + "  | " + inputSignals[1] + ")";
                        break;
                    case "XOR":
                        if (inputSignals.Count != 2)
                            throw new InvalidOperationException("XOR gate requires exactly 2 inputs.");
                        expression = inputSignals[0] + " ^ " + inputSignals[1];
                        break;
                    default:
                        throw new InvalidOperationException("Gate type " + gateItem.GateType + " is not supported.");
                }

                builder.AppendLine("assign " + signalNames[gateItem] + " = " + expression + ";");
            }

            foreach (var lampItem in Lamps)
            {
                string sourceName = GetInputSignalName(manager, lampItem, 0, signalNames);
                builder.AppendLine("assign " + signalNames[lampItem] + " = " + sourceName + ";");
            }

            builder.AppendLine("endmodule");

            return builder.ToString();
        }

        private string GetInputSignalName(CircuitManager manager, IGate targetGate, int inputIndex, Dictionary<IGate, string> signalNames)
        {
            var matchingWires = manager.Wires.Where(x => x.TargetGate == targetGate && x.TargetInputIndex == inputIndex).ToList();

            if (matchingWires.Count == 0)
            {
                throw new InvalidOperationException($"Input {inputIndex} on gate {targetGate.Id} is not connected");
            }
            if (matchingWires.Count > 1)
            {
                throw new InvalidOperationException($"More than one wire connected to input {inputIndex} on gate {targetGate.Id}.");
            }

            IGate sourceGate = matchingWires[0].SourceGate;

            if (!signalNames.ContainsKey(sourceGate))
            {
                throw new InvalidOperationException($"Source gate {sourceGate.Id} was not found in signal definitions.");
            }

            return signalNames[sourceGate];
        }
        private void ValidateModuleName(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                throw new ArgumentException("Module name is required", nameof(moduleName));
            }
            char firstCharacter = moduleName[0];
            bool validFirstCharacter = (firstCharacter >= 'a' && firstCharacter <= 'z') || (firstCharacter >= 'A' && firstCharacter <= 'Z') || firstCharacter == '_';
            if (validFirstCharacter == false)

            {

                throw new ArgumentException("Module name must start with an ASCII letter or underscore.", nameof(moduleName));
            }

            for (int characterIndex = 1; characterIndex < moduleName.Length; characterIndex++)
            {
                char currentCharacter = moduleName[characterIndex];
                bool validCharacter = (currentCharacter >= 'a' && currentCharacter <= 'z') || (currentCharacter >= 'A' && currentCharacter <= 'Z') || currentCharacter == '_' || (currentCharacter >= '0' && currentCharacter <= '9') || currentCharacter == '$';
                if (validCharacter == false)
                {
                    throw new ArgumentException("Module name may contain only ASCII letters, digits, underscores, and dollar signs.", nameof(moduleName));
                }


            }
            if (reservedKeywords.Contains(moduleName))
            {
                throw new ArgumentException("Module name cannot be a reserved keyword.", nameof(moduleName));
            }
        }



    }
}
