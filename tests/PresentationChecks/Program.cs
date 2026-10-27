using BlazorApp1.Presentation;
using LogicCore.Connections;
using LogicCore.Gates;
using LogicCore.Serialization;
using Stopwatch = System.Diagnostics.Stopwatch;

int checks = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception(name);
    checks++;
    Console.WriteLine("PASS " + name);
}

string root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "LogiclyInspiredSimulator.slnx")))
    root = Directory.GetParent(root)?.FullName ?? throw new Exception("Project root was not found.");
var manager = CircuitSerializer.FromJsonToManager(File.ReadAllText(Path.Combine(root, "examples/full_adder.circuit.json")));
string before = CircuitSerializer.ToJson(manager);
var boxes = manager.Gates.Select(g => new BoardBox(g.X - 8, g.Y - 8, g.X + 118, g.Y + 76)).ToArray();
var endpoints = manager.Wires.Select(w => new WireEndpoints(w,
    new BoardPoint(w.SourceGate.X + 110, w.SourceGate.Y + 34),
    new BoardPoint(w.TargetGate.X, w.TargetGate.Y + 68.0 * (w.TargetInputIndex + 1) / (w.TargetGate.Inputs.Length + 1)))).ToArray();
var timer = Stopwatch.StartNew();
var routes = WireLayout.Build(endpoints, boxes);
timer.Stop();
Check("Full adder keeps all twelve wires", routes.Count == 12);
Check("Routing leaves circuit data unchanged", before == CircuitSerializer.ToJson(manager));
Check("Routes preserve their wire identities", routes.Select(r => r.Wire).SequenceEqual(manager.Wires));
Check("Every route begins and ends at its pins", routes.Select((r, i) => r.Points[0] == endpoints[i].Start && r.Points[^1] == endpoints[i].End).All(x => x));
Check("All full-adder segments are orthogonal", routes.SelectMany(r => WireLayout.Segments(r.Points)).All(s => s.A.X == s.B.X || s.A.Y == s.B.Y));
Check("Full-adder routes contain no duplicate adjacent points", routes.All(r => WireLayout.Segments(r.Points).All(s => s.A != s.B)));
Check("Internal segments avoid gate boxes", routes.All(r => WireLayout.Segments(r.Points).Skip(1).SkipLast(1).All(s => !boxes.Any(b => WireLayout.IntersectsBox(s.A, s.B, b)))));
bool sharedInteriors = false;
for (int i = 0; i < routes.Count; i++)
    for (int j = 0; j < i; j++)
        if (!ReferenceEquals(routes[i].Wire.SourceGate, routes[j].Wire.SourceGate))
            foreach (var a in WireLayout.Segments(routes[i].Points).Skip(1).SkipLast(1))
                foreach (var b in WireLayout.Segments(routes[j].Points).Skip(1).SkipLast(1))
                    sharedInteriors |= WireLayout.OverlapLength(a.A, a.B, b.A, b.B) > .001;
Check("Different full-adder sources have separate interior lanes", !sharedInteriors);
Check("Routing is repeatable", routes.Zip(WireLayout.Build(endpoints, boxes)).All(p => p.First.Points.SequenceEqual(p.Second.Points)));
Check("Empty board has no routes", WireLayout.Build(Array.Empty<WireEndpoints>(), Array.Empty<BoardBox>()).Count == 0);
var source = new Switch { Id = "source" };
var target = new Lamp { Id = "target" };
var wire = new Wire(source, target, 0);
var backward = new WireEndpoints(wire, new BoardPoint(300, 100), new BoardPoint(0, 100));
var backRoute = WireLayout.Build(new[] { backward }, Array.Empty<BoardBox>())[0];
Check("Backward route preserves source and target leads", backRoute.Points.Count >= 4 && backRoute.Points[1].X > 300 && backRoute.Points[^2].X < 0);
Check("Backward route remains orthogonal", WireLayout.Segments(backRoute.Points).All(s => s.A.X == s.B.X || s.A.Y == s.B.Y));
Console.WriteLine($"FULL_ADDER_ROUTING_MS={timer.Elapsed.TotalMilliseconds:F2}");
Console.WriteLine($"PRESENTATION_CHECKS_PASSED={checks}");
