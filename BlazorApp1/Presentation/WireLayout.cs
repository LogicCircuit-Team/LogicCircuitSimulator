using LogicCore.Connections;

namespace BlazorApp1.Presentation;

public readonly record struct BoardPoint(double X, double Y);
public readonly record struct BoardBox(double Left, double Top, double Right, double Bottom);
public sealed record WireEndpoints(Wire Wire, BoardPoint Start, BoardPoint End);
public sealed record WireRoute(Wire Wire, IReadOnlyList<BoardPoint> Points);

public sealed record BoardScene(
    IReadOnlyList<WireRoute> Routes, double Zoom, double PanX, double PanY,
    double Width, double Height, Wire? Selected);

public static class WireLayout
{
    private const double Spacing = 14.0;
    private const double Eps = 0.001;
    private const int MaxExaminedNodes = 25000;

    private readonly record struct SearchNode(int X, int Y, int Direction);
    private readonly record struct Segment(BoardPoint A, BoardPoint B);

    public static List<WireRoute> Build(IReadOnlyList<WireEndpoints> wires, IReadOnlyList<BoardBox> gates)
    {
        var result = new List<WireRoute>(wires.Count);
        var allOccupiedSegments = new List<Segment>(wires.Count * 4);

        for (int i = 0; i < wires.Count; i++)
        {
            var wire = wires[i];
            var points = FindPath(wire, gates, allOccupiedSegments, i);
            result.Add(new WireRoute(wire.Wire, points));

            for (int p = 1; p < points.Count; p++)
            {
                allOccupiedSegments.Add(new Segment(points[p - 1], points[p]));
            }
        }

        return result;
    }

    private static IReadOnlyList<BoardPoint> FindPath(
        WireEndpoints wire,
        IReadOnlyList<BoardBox> gates,
        List<Segment> allOccupiedSegments,
        int existingWireCount)
    {
        var start = new BoardPoint(wire.Start.X + 24, wire.Start.Y);
        var end = new BoardPoint(wire.End.X - 24, wire.End.Y);

        var left = Math.Min(start.X, end.X) - 140;
        var right = Math.Max(start.X, end.X) + 140;
        var top = Math.Min(start.Y, end.Y) - 140;
        var bottom = Math.Max(start.Y, end.Y) + 140;

        var nearby = new List<BoardBox>();
        for (int i = 0; i < gates.Count; i++)
        {
            var g = gates[i];
            if (g.Right >= left && g.Left <= right && g.Bottom >= top && g.Top <= bottom)
            {
                nearby.Add(g);
            }
        }

        var xs = new SortedSet<double> { start.X, end.X, left, right };
        var ys = new SortedSet<double> { start.Y, end.Y, top, bottom };

        foreach (var g in nearby)
        {
            xs.Add(g.Left - Spacing);
            xs.Add(g.Right + Spacing);
            ys.Add(g.Top - Spacing);
            ys.Add(g.Bottom + Spacing);
        }

        ReadOnlySpan<int> offsets = [-2, -1, 1, 2];
        foreach (var offset in offsets)
        {
            double delta = offset * Spacing;
            xs.Add(start.X + delta);
            xs.Add(end.X + delta);
            ys.Add(start.Y + delta);
            ys.Add(end.Y + delta);
        }

        var x = xs.ToArray();
        var y = ys.ToArray();

        int startXIdx = Array.BinarySearch(x, start.X);
        int startYIdx = Array.BinarySearch(y, start.Y);
        int targetX = Array.BinarySearch(x, end.X);
        int targetY = Array.BinarySearch(y, end.Y);

        if (startXIdx < 0 || startYIdx < 0 || targetX < 0 || targetY < 0)
        {
            var fallbackChannel = top - existingWireCount * Spacing;
            return Simplify([wire.Start, start, new BoardPoint(start.X, fallbackChannel), new BoardPoint(end.X, fallbackChannel), end, wire.End]);
        }

        var occupied = new List<Segment>();
        for (int i = 0; i < allOccupiedSegments.Count; i++)
        {
            var edge = allOccupiedSegments[i];
            if (Math.Max(edge.A.X, edge.B.X) >= left && Math.Min(edge.A.X, edge.B.X) <= right &&
                Math.Max(edge.A.Y, edge.B.Y) >= top && Math.Min(edge.A.Y, edge.B.Y) <= bottom)
            {
                occupied.Add(edge);
            }
        }

        var first = new SearchNode(startXIdx, startYIdx, 0);
        var queue = new PriorityQueue<SearchNode, double>();
        var costs = new Dictionary<SearchNode, double> { [first] = 0 };
        var parents = new Dictionary<SearchNode, SearchNode>();

        queue.Enqueue(first, Math.Abs(start.X - end.X) + Math.Abs(start.Y - end.Y));
        int examined = 0;

        ReadOnlySpan<(int Dx, int Dy)> dirs = [(1, 0), (0, 1), (-1, 0), (0, -1)];

        while (queue.TryDequeue(out var current, out var priority) && examined++ < MaxExaminedNodes)
        {
            if (current.X == targetX && current.Y == targetY)
            {
                var points = new List<BoardPoint> { wire.End, end };
                for (var node = current; node != first; node = parents[node])
                {
                    points.Add(new BoardPoint(x[node.X], y[node.Y]));
                }
                points.Add(start);
                points.Add(wire.Start);
                points.Reverse();
                return Simplify(points);
            }

            var currentCost = costs[current];
            var a = new BoardPoint(x[current.X], y[current.Y]);
            double heuristicCurrent = Math.Abs(a.X - end.X) + Math.Abs(a.Y - end.Y);
            if (priority > currentCost + heuristicCurrent + Eps)
            {
                continue; // تخطي العقد القديمة في PriorityQueue
            }

            foreach (var (dx, dy) in dirs)
            {
                int nx = current.X + dx;
                int ny = current.Y + dy;
                if (nx < 0 || nx >= x.Length || ny < 0 || ny >= y.Length)
                    continue;

                var b = new BoardPoint(x[nx], y[ny]);

                bool hitsGate = false;
                for (int i = 0; i < nearby.Count; i++)
                {
                    if (IntersectsBox(a, b, nearby[i]))
                    {
                        hitsGate = true;
                        break;
                    }
                }
                if (hitsGate) continue;

                int direction = dx == 0 ? 2 : 1;
                var next = new SearchNode(nx, ny, direction);
                double length = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

                double overlap = 0;
                int crossings = 0;
                for (int i = 0; i < occupied.Count; i++)
                {
                    var edge = occupied[i];
                    overlap += OverlapLength(a, b, edge.A, edge.B);
                    if (Crosses(a, b, edge.A, edge.B))
                        crossings++;
                }

                double cost = currentCost + length + overlap * 80.0 + crossings * 28.0;
                if (current.Direction != 0 && current.Direction != direction)
                    cost += 18.0;

                if (costs.TryGetValue(next, out var oldCost) && cost >= oldCost - Eps)
                    continue;

                costs[next] = cost;
                parents[next] = current;
                double h = Math.Abs(b.X - end.X) + Math.Abs(b.Y - end.Y);
                queue.Enqueue(next, cost + h);
            }
        }

        var channel = top - existingWireCount * Spacing;
        return Simplify([wire.Start, start, new BoardPoint(start.X, channel), new BoardPoint(end.X, channel), end, wire.End]);
    }

    public static bool IntersectsBox(BoardPoint a, BoardPoint b, BoardBox box)
    {
        if (Math.Abs(a.Y - b.Y) < Eps)
            return a.Y > box.Top && a.Y < box.Bottom && Math.Max(a.X, b.X) > box.Left && Math.Min(a.X, b.X) < box.Right;
        return a.X > box.Left && a.X < box.Right && Math.Max(a.Y, b.Y) > box.Top && Math.Min(a.Y, b.Y) < box.Bottom;
    }

    public static double OverlapLength(BoardPoint a, BoardPoint b, BoardPoint c, BoardPoint d)
    {
        if (Math.Abs(a.Y - b.Y) < Eps && Math.Abs(c.Y - d.Y) < Eps && Math.Abs(a.Y - c.Y) < Eps)
            return Math.Max(0, Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) - Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)));
        if (Math.Abs(a.X - b.X) < Eps && Math.Abs(c.X - d.X) < Eps && Math.Abs(a.X - c.X) < Eps)
            return Math.Max(0, Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) - Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)));
        return 0;
    }

    private static bool Crosses(BoardPoint a, BoardPoint b, BoardPoint c, BoardPoint d)
    {
        if (Math.Abs(a.Y - b.Y) >= Eps)
            (a, b, c, d) = (c, d, a, b);

        return Math.Abs(a.Y - b.Y) < Eps &&
               Math.Abs(c.X - d.X) < Eps &&
               c.X > Math.Min(a.X, b.X) + Eps && c.X < Math.Max(a.X, b.X) - Eps &&
               a.Y > Math.Min(c.Y, d.Y) + Eps && a.Y < Math.Max(c.Y, d.Y) - Eps;
    }

    public static IEnumerable<(BoardPoint A, BoardPoint B)> Segments(IReadOnlyList<BoardPoint> points)
    {
        for (var i = 1; i < points.Count; i++)
            yield return (points[i - 1], points[i]);
    }

    private static IReadOnlyList<BoardPoint> Simplify(IEnumerable<BoardPoint> input)
    {
        var result = new List<BoardPoint>();
        foreach (var point in input)
        {
            if (result.Count > 0 && SamePoint(result[^1], point))
                continue;
            while (result.Count > 1 && Continues(result[^2], result[^1], point))
                result.RemoveAt(result.Count - 1);
            result.Add(point);
        }
        return result;
    }

    private static bool SamePoint(BoardPoint a, BoardPoint b) =>
        Math.Abs(a.X - b.X) < Eps && Math.Abs(a.Y - b.Y) < Eps;

    private static bool Continues(BoardPoint a, BoardPoint b, BoardPoint c) =>
        (Math.Abs(a.X - b.X) < Eps && Math.Abs(b.X - c.X) < Eps && (b.Y - a.Y) * (c.Y - b.Y) >= -Eps) ||
        (Math.Abs(a.Y - b.Y) < Eps && Math.Abs(b.Y - c.Y) < Eps && (b.X - a.X) * (c.X - b.X) >= -Eps);
}