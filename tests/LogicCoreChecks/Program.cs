using LogicCore.Analysis;
using LogicCore.Gates;
using LogicCore.Management;
using LogicCore.Serialization;
using System.Text.Json.Nodes;

string root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "LogiclyInspiredSimulator.slnx")))
    root = Directory.GetParent(root)?.FullName ?? throw new Exception("Run the checks from the project checkout.");
string tool = Path.Combine(root, "tests/Fixtures");
int checks = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception(name);
    checks++;
    Console.WriteLine("PASS " + name);
}
void CheckFullAdder(CircuitManager manager, string label)
{
    var switches = manager.Gates.OfType<Switch>().OrderBy(s => s.Id, StringComparer.Ordinal).ToArray();
    for (int i = 0; i < switches.Length; i++) switches[i].State = i % 2 == 0;
    manager.Simulate();
    var original = switches.Select(s => s.State).ToArray();
    var output = manager.Gates.OfType<Lamp>().Select(l => l.Output).ToArray();
    var generator = new TruthTableGenerator();
    var rows = generator.Generate(manager);
    Check(label + ": 8 truth table rows", rows.Count == 8);
    foreach (var row in rows)
    {
        int count = row.InputValues.Count(v => v);
        int sumIndex = Array.FindIndex(generator.OutputIds, id => id.EndsWith("sum", StringComparison.Ordinal));
        int carryIndex = Array.FindIndex(generator.OutputIds, id => id.EndsWith("cout", StringComparison.Ordinal));
        Check(label + ": row " + string.Concat(row.InputValues.Select(v => v ? "1" : "0")), row.OutputValues[sumIndex] == (count % 2 == 1) && row.OutputValues[carryIndex] == (count >= 2));
    }
    Check(label + ": input states restored", original.SequenceEqual(switches.Select(s => s.State)));
    Check(label + ": lamp states restored", output.SequenceEqual(manager.Gates.OfType<Lamp>().Select(l => l.Output)));
}

var adapter = new YosysNetlistAdapter();
string netlist = File.ReadAllText(Path.Combine(tool, "full-adder-netlist.json"));
var imported = adapter.ImportFromJson(netlist, "full_adder");
Check("Yosys full adder has 10 gates and 12 wires", imported.Gates.Count == 10 && imported.Wires.Count == 12);
CheckFullAdder(imported, "Real Yosys import");
CheckFullAdder(CircuitSerializer.FromJsonToManager(CircuitSerializer.ToJson(imported)), "JSON round trip");
CheckFullAdder(CircuitSerializer.FromJsonToManager(File.ReadAllText(Path.Combine(root, "examples/full_adder.circuit.json"))), "Bundled JSON example");

var originalGates = imported.Gates.ToArray();
var broken = new CircuitManager();
var and = new AndGate { Id = "loop" };
broken.Gates.Add(and);
broken.Wires.Add(new LogicCore.Connections.Wire(and, and, 0));
try
{
    imported.ReplaceWith(broken);
    throw new Exception("Cycle was accepted");
}
catch (InvalidOperationException)
{
    Check("Rejected cycle preserves existing circuit", originalGates.SequenceEqual(imported.Gates));
}

var malformed = JsonNode.Parse(netlist)!;
var cells = malformed["modules"]!["full_adder"]!["cells"]!.AsObject();
cells.First().Value!["type"] = "$_DFF_P_";
try
{
    adapter.ImportFromJson(malformed.ToJsonString(), "full_adder");
    throw new Exception("Unsupported cell was accepted");
}
catch (InvalidOperationException)
{
    Check("Unsupported sequential cell rejected", true);
}

var translator = new VerilogTranslator();
string exported = translator.ExportToVerilog(imported, "round_trip", true);

Check("Export has one module and assignments", exported.StartsWith("module round_trip(") && exported.Contains("assign ") && exported.TrimEnd().EndsWith("endmodule"));
var gates = new (LogicCore.Interfaces.IGate Gate, Func<bool, bool, bool> Expected)[]
{
    (new AndGate(), (a, b) => a && b),
    (new ORGate(), (a, b) => a || b),
    (new NotGate(), (a, b) => !a),
    (new NANDGate(), (a, b) => !(a && b)),
    (new NOR(), (a, b) => !(a || b)),
    (new XORGate(), (a, b) => a != b)
};
foreach (var test in gates)
{
    for (int state = 0; state < (test.Gate.Inputs.Length == 1 ? 2 : 4); state++)
    {
        bool a = (state & 1) != 0;
        bool b = (state & 2) != 0;
        test.Gate.Inputs[0] = a;
        if (test.Gate.Inputs.Length == 2) test.Gate.Inputs[1] = b;
        test.Gate.Evaluate();
        Check(test.Gate.GateType + " input pattern " + state, test.Gate.Output == test.Expected(a, b));
    }
}
var large = new CircuitManager();
var input = new Switch { Id = "benchmark-input" };
large.Gates.Add(input);
LogicCore.Interfaces.IGate previous = input;
for (int i = 0; i < 1000; i++)
{
    var next = new NotGate { Id = "benchmark-" + i };
    large.Gates.Add(next);
    large.Wires.Add(new LogicCore.Connections.Wire(previous, next, 0));
    previous = next;
}
var lamp = new Lamp { Id = "benchmark-output" };
large.Gates.Add(lamp);
large.Wires.Add(new LogicCore.Connections.Wire(previous, lamp, 0));
large.Simulate();
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
for (int i = 0; i < 200; i++)
{
    input.State = i % 2 == 0;
    large.Simulate();
    if (lamp.Output != input.State) throw new Exception("Benchmark output mismatch");
}
stopwatch.Stop();
Check("Cached simulation preserves a 1000-gate chain", true);
Console.WriteLine($"BENCHMARK_1000_GATES_200_UPDATES_MS={stopwatch.Elapsed.TotalMilliseconds:F2}");
Console.WriteLine($"CORE_CHECKS_PASSED={checks}");
