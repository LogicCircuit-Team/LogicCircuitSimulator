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
    private const double Spacing = 14;
    private readonly record struct SearchNode(int X, int Y, int Direction);

    public static List<WireRoute> Build(IReadOnlyList<WireEndpoints> wires, IReadOnlyList<BoardBox> gates)
    {
        var result = new List<WireRoute>();
        foreach (var wire in wires)
        {
            var points = FindPath(wire, gates, result);
            result.Add(new WireRoute(wire.Wire, points));
        }
        return result;
    }

    private static IReadOnlyList<BoardPoint> FindPath(WireEndpoints wire, IReadOnlyList<BoardBox> gates, IReadOnlyList<WireRoute> existing)
    {
        var start = new BoardPoint(wire.Start.X + 24, wire.Start.Y);
        var end = new BoardPoint(wire.End.X - 24, wire.End.Y);
        var left = Math.Min(start.X, end.X) - 140;
        var right = Math.Max(start.X, end.X) + 140;
        var top = Math.Min(start.Y, end.Y) - 140;
        var bottom = Math.Max(start.Y, end.Y) + 140;
        var nearby = gates.Where(g => g.Right >= left && g.Left <= right && g.Bottom >= top && g.Top <= bottom).ToArray();
        var xs = new SortedSet<double> { start.X, end.X, left, right };
        var ys = new SortedSet<double> { start.Y, end.Y, top, bottom };
        foreach (var g in nearby)
        {
            xs.Add(g.Left - Spacing);
            xs.Add(g.Right + Spacing);
            ys.Add(g.Top - Spacing);
            ys.Add(g.Bottom + Spacing);
        }
        foreach (var offset in new[] { -2, -1, 1, 2 })
        {
            xs.Add(start.X + offset * Spacing);
            xs.Add(end.X + offset * Spacing);
            ys.Add(start.Y + offset * Spacing);
            ys.Add(end.Y + offset * Spacing);
        }
        var x = xs.ToArray();
        var y = ys.ToArray();
        var first = new SearchNode(Array.IndexOf(x, start.X), Array.IndexOf(y, start.Y), 0);
        var targetX = Array.IndexOf(x, end.X);
        var targetY = Array.IndexOf(y, end.Y);
        var queue = new PriorityQueue<SearchNode, double>();
        var costs = new Dictionary<SearchNode, double> { [first] = 0 };
        var parents = new Dictionary<SearchNode, SearchNode>();
        var occupied = existing.SelectMany(route => Segments(route.Points))
            .Where(edge => Math.Max(edge.A.X, edge.B.X) >= left && Math.Min(edge.A.X, edge.B.X) <= right &&
                Math.Max(edge.A.Y, edge.B.Y) >= top && Math.Min(edge.A.Y, edge.B.Y) <= bottom).ToArray();
        queue.Enqueue(first, 0);
        var examined = 0;
        while (queue.TryDequeue(out var current, out _) && examined++ < 30000)
        {
            if (current.X == targetX && current.Y == targetY)
            {
                var points = new List<BoardPoint> { wire.End, end };
                for (var node = current; node != first; node = parents[node])
                    points.Add(new BoardPoint(x[node.X], y[node.Y]));
                points.Add(start);
                points.Add(wire.Start);
                points.Reverse();
                return Simplify(points);
            }
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
            {
                var nx = current.X + dx;
                var ny = current.Y + dy;
                if (nx < 0 || nx >= x.Length || ny < 0 || ny >= y.Length) continue;
                var a = new BoardPoint(x[current.X], y[current.Y]);
                var b = new BoardPoint(x[nx], y[ny]);
                if (nearby.Any(g => IntersectsBox(a, b, g))) continue;
                var direction = dx == 0 ? 2 : 1;
                var next = new SearchNode(nx, ny, direction);
                var length = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
                var overlap = occupied.Sum(edge => OverlapLength(a, b, edge.A, edge.B));
                var crossings = occupied.Count(edge => Crosses(a, b, edge.A, edge.B));
                var cost = costs[current] + length + overlap * 80 + crossings * 28;
                if (current.Direction != 0 && current.Direction != direction) cost += 18;
                if (costs.TryGetValue(next, out var oldCost) && cost >= oldCost) continue;
                costs[next] = cost;
                parents[next] = current;
                queue.Enqueue(next, cost + Math.Abs(b.X - end.X) + Math.Abs(b.Y - end.Y));
            }
        }
        var channel = top - existing.Count * Spacing;
        return Simplify(new[] { wire.Start, start, new BoardPoint(start.X, channel), new BoardPoint(end.X, channel), end, wire.End });
    }

    public static bool IntersectsBox(BoardPoint a, BoardPoint b, BoardBox box)
    {
        if (Math.Abs(a.Y - b.Y) < .001)
            return a.Y > box.Top && a.Y < box.Bottom && Math.Max(a.X, b.X) > box.Left && Math.Min(a.X, b.X) < box.Right;
        return a.X > box.Left && a.X < box.Right && Math.Max(a.Y, b.Y) > box.Top && Math.Min(a.Y, b.Y) < box.Bottom;
    }

    public static double OverlapLength(BoardPoint a, BoardPoint b, BoardPoint c, BoardPoint d)
    {
        if (Math.Abs(a.Y - b.Y) < .001 && Math.Abs(c.Y - d.Y) < .001 && Math.Abs(a.Y - c.Y) < .001)
            return Math.Max(0, Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) - Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)));
        if (Math.Abs(a.X - b.X) < .001 && Math.Abs(c.X - d.X) < .001 && Math.Abs(a.X - c.X) < .001)
            return Math.Max(0, Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) - Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)));
        return 0;
    }

    private static bool Crosses(BoardPoint a, BoardPoint b, BoardPoint c, BoardPoint d)
    {
        if (a.Y != b.Y) (a, b, c, d) = (c, d, a, b);
        return a.Y == b.Y && c.X == d.X && c.X > Math.Min(a.X, b.X) && c.X < Math.Max(a.X, b.X) && a.Y > Math.Min(c.Y, d.Y) && a.Y < Math.Max(c.Y, d.Y);
    }

    public static IEnumerable<(BoardPoint A, BoardPoint B)> Segments(IReadOnlyList<BoardPoint> points)
    {
        for (var i = 1; i < points.Count; i++) yield return (points[i - 1], points[i]);
    }

    private static IReadOnlyList<BoardPoint> Simplify(IEnumerable<BoardPoint> input)
    {
        var result = new List<BoardPoint>();
        foreach (var point in input)
        {
            if (result.Count > 0 && result[^1] == point) continue;
            while (result.Count > 1 && Continues(result[^2], result[^1], point)) result.RemoveAt(result.Count - 1);
            result.Add(point);
        }
        return result;
    }

    private static bool Continues(BoardPoint a, BoardPoint b, BoardPoint c) =>
        (a.X == b.X && b.X == c.X && (b.Y - a.Y) * (c.Y - b.Y) >= 0) ||
        (a.Y == b.Y && b.Y == c.Y && (b.X - a.X) * (c.X - b.X) >= 0);
}
