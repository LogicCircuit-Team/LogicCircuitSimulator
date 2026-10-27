using System.Globalization;
using BlazorApp1.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorApp1.Pages;

public partial class CanvasBoard
{
    private int? presentationGeometry;
    private List<WireRoute> presentationRoutes = new();

    private BoardScene GetPresentationScene()
    {
        var hash = new HashCode();
        foreach (var gate in Manager.Gates)
        {
            hash.Add(gate);
            hash.Add(gate.X);
            hash.Add(gate.Y);
            hash.Add(gate.Inputs.Length);
        }
        foreach (var wire in Manager.Wires)
        {
            hash.Add(wire);
            hash.Add(wire.SourceGate);
            hash.Add(wire.TargetGate);
            hash.Add(wire.TargetInputIndex);
        }
        var key = hash.ToHashCode();
        if (presentationGeometry != key)
        {
            var boxes = Manager.Gates.Select(g => new BoardBox(g.X - 8, g.Y - 8, g.X + GateWidth + 8, g.Y + GateHeight + 8)).ToArray();
            var wires = Manager.Wires.Select(w =>
            {
                var start = GetOutputPinPosition(w.SourceGate);
                var end = GetInputPinPosition(w.TargetGate, w.TargetInputIndex);
                return new WireEndpoints(w, new BoardPoint(start.X, start.Y), new BoardPoint(end.X, end.Y));
            }).ToArray();
            presentationRoutes = WireLayout.Build(wires, boxes);
            presentationGeometry = key;
        }
        return new BoardScene(presentationRoutes, _zoom, _panX, _panY, _canvasWidth, _canvasHeight, _selectedWire);
    }

    private async Task HandlePresentationMouseDownAsync(MouseEventArgs e)
    {
        var (x, y) = ScreenToCircuit(e.OffsetX, e.OffsetY);
        if (_isPanMode || e.Button == 1 || TryFindOutputPinAt(x, y, out _) || TryFindInputPinAt(x, y, out _, out _) || FindGateAt(x, y) != null)
        {
            await HandleMouseDownAsync(e);
            return;
        }
        GetPresentationScene();
        var tolerance = (_isTouchInput ? 14 : 8) / _zoom;
        var route = presentationRoutes.LastOrDefault(r => WireLayout.Segments(r.Points).Any(s => DistanceToSegment(x, y, s.A.X, s.A.Y, s.B.X, s.B.Y) <= tolerance));
        if (route != null && Manager.Wires.Contains(route.Wire))
        {
            if (e.Button == 2)
            {
                RemoveWireSafely(route.Wire);
                SetStatus("Wire deleted.", false);
                await NotifyUserModificationAsync();
            }
            else
            {
                _selectedWire = route.Wire;
                _selectedGate = null;
                SetStatus("Wire selected. Use Delete selected to remove it.", false);
                await RefreshAsync();
            }
            return;
        }
        if (e.Button != 0) return;
        _selectedGate = null;
        _selectedWire = null;
        _isPanning = true;
        _panStartMouseX = e.OffsetX - _panX;
        _panStartMouseY = e.OffsetY - _panY;
        await RefreshAsync();
    }

    private async Task HandlePresentationTouchStartAsync(TouchEventArgs e)
    {
        if (e.Touches.Length != 1) return;
        _isTouchInput = true;
        _touchActive = true;
        await HandlePresentationMouseDownAsync(await GetTouchPositionAsync(e.Touches[0]));
    }

    private double PanMinimumX => Math.Min(-1000, Manager.Gates.Count == 0 ? 0 : Manager.Gates.Min(g => g.X) - 600);
    private double PanMaximumX => Math.Max(2000, Manager.Gates.Count == 0 ? 0 : Manager.Gates.Max(g => g.X) + 600);
    private double PanMinimumY => Math.Min(-1000, Manager.Gates.Count == 0 ? 0 : Manager.Gates.Min(g => g.Y) - 600);
    private double PanMaximumY => Math.Max(2000, Manager.Gates.Count == 0 ? 0 : Manager.Gates.Max(g => g.Y) + 600);
    private static string Coordinate(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private async Task SlideHorizontalAsync(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            _panX = -value * _zoom;
            await RefreshAsync();
        }
    }

    private async Task SlideVerticalAsync(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            _panY = -value * _zoom;
            await RefreshAsync();
        }
    }
}
