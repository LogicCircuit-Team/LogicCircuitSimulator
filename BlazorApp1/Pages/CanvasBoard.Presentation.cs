using System.Globalization;
using System.Text.Json.Serialization;
using BlazorApp1.Presentation;
using LogicCore.Gates;
using LogicCore.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorApp1.Pages;

public partial class CanvasBoard
{
    private int? _presentationGeometryHash;
    private List<WireRoute> _presentationRoutes = new();
    private bool _initialMobileFitDone;

    private BoardScene GetPresentationScene()
    {
        if (Manager == null)
        {
            return new BoardScene(Array.Empty<WireRoute>(), _zoom, _panX, _panY, _canvasWidth, _canvasHeight, _selectedWire);
        }

        var hash = new HashCode();
        foreach (var gate in Manager.Gates)
        {
            if (gate == null) continue;
            hash.Add(gate);
            hash.Add(gate.X);
            hash.Add(gate.Y);
            hash.Add(gate.Inputs?.Length ?? 0);
        }

        foreach (var wire in Manager.Wires)
        {
            if (wire?.SourceGate == null || wire.TargetGate == null) continue;
            hash.Add(wire);
            hash.Add(wire.SourceGate);
            hash.Add(wire.TargetGate);
            hash.Add(wire.TargetInputIndex);
        }

        var key = hash.ToHashCode();
        if (_presentationGeometryHash != key)
        {
            var validGates = Manager.Gates.Where(g => g != null).ToArray();
            var gateSet = new HashSet<IGate>(validGates, ReferenceEqualityComparer.Instance);

            var boxes = validGates
                .Select(g => new BoardBox(g.X - 8, g.Y - 8, g.X + GateWidth + 8, g.Y + GateHeight + 8))
                .ToArray();

            var wires = Manager.Wires
     .Where(w => w != null && w.SourceGate != null && w.TargetGate != null &&
                 gateSet.Contains(w.SourceGate) && gateSet.Contains(w.TargetGate) &&
                 w.TargetGate.Inputs != null &&
                 w.TargetInputIndex >= 0 && w.TargetInputIndex < w.TargetGate.Inputs.Length)
                 .Select(w =>
                {
                    var start = GetOutputPinPosition(w.SourceGate);
                    var end = GetInputPinPosition(w.TargetGate, w.TargetInputIndex);
                    return new WireEndpoints(w, new BoardPoint(start.X, start.Y), new BoardPoint(end.X, end.Y));
                })
                .ToArray();

            _presentationRoutes = WireLayout.Build(wires, boxes);
            _presentationGeometryHash = key;
        }

        return new BoardScene(_presentationRoutes, _zoom, _panX, _panY, _canvasWidth, _canvasHeight, _selectedWire);
    }

    private CanvasScenePayload BuildScenePayload()
    {
        var scene = GetPresentationScene();
        var validGates = Manager.Gates.Where(g => g != null).ToArray();

        var gateDtos = new GateRenderDto[validGates.Length];
        for (int i = 0; i < validGates.Length; i++)
        {
            var g = validGates[i];
            gateDtos[i] = new GateRenderDto
            {
                Id = g.Id,
                Type = g.GateType ?? string.Empty,
                Label = FormatGateLabel(g),
                X = g.X,
                Y = g.Y,
                Inputs = g.Inputs ?? Array.Empty<bool>(),
                Output = g.Output
            };
        }

        var wireDtos = new WireRenderDto[scene.Routes.Count];
        int selectedWireIndex = -1;

        for (int i = 0; i < scene.Routes.Count; i++)
        {
            var route = scene.Routes[i];
            if (ReferenceEquals(route.Wire, _selectedWire))
            {
                selectedWireIndex = i;
            }

            var flatPoints = new double[route.Points.Count * 2];
            for (int p = 0; p < route.Points.Count; p++)
            {
                flatPoints[p * 2] = route.Points[p].X;
                flatPoints[p * 2 + 1] = route.Points[p].Y;
            }

            wireDtos[i] = new WireRenderDto
            {
                SourceId = route.Wire.SourceGate.Id,
                TargetId = route.Wire.TargetGate.Id,
                TargetInput = route.Wire.TargetInputIndex,
                High = route.Wire.SourceGate.Output,
                Points = flatPoints
            };
        }

        return new CanvasScenePayload
        {
            PanX = _panX,
            PanY = _panY,
            Zoom = _zoom,
            IsPanMode = _isPanMode,
            SelectedGateId = _selectedGate?.Id,
            SelectedWireIndex = selectedWireIndex,
            Gates = gateDtos,
            Wires = wireDtos
        };
    }

    /// <summary>
    /// Fast O(V + E) Signal Delta Payload when only signal states (0/1) or selection changed,
    /// avoiding serializing geometry or re-running A* routing.
    /// </summary>
    private SignalDeltaPayload BuildSignalDeltaPayload()
    {
        var scene = GetPresentationScene();
        var validGates = Manager.Gates.Where(g => g != null).ToArray();

        var gateStates = new GateSignalDto[validGates.Length];
        for (int i = 0; i < validGates.Length; i++)
        {
            var g = validGates[i];
            gateStates[i] = new GateSignalDto
            {
                Id = g.Id,
                Inputs = g.Inputs ?? Array.Empty<bool>(),
                Output = g.Output
            };
        }

        var wireHighs = new bool[scene.Routes.Count];
        int selectedWireIndex = -1;
        for (int i = 0; i < scene.Routes.Count; i++)
        {
            var route = scene.Routes[i];
            wireHighs[i] = route.Wire.SourceGate.Output;
            if (ReferenceEquals(route.Wire, _selectedWire))
            {
                selectedWireIndex = i;
            }
        }

        return new SignalDeltaPayload
        {
            SelectedGateId = _selectedGate?.Id,
            SelectedWireIndex = selectedWireIndex,
            Gates = gateStates,
            WireHighs = wireHighs
        };
    }

    private static string FormatGateLabel(IGate gate)
    {
        string label = gate.GateType ?? string.Empty;
        if (gate is Switch && gate.Id.StartsWith("input_", StringComparison.Ordinal))
            label = gate.Id[6..];
        else if (gate is Lamp && gate.Id.StartsWith("output_", StringComparison.Ordinal))
            label = gate.Id[7..];

        if (label.Length > 18)
            label = label[..15] + "...";

        return label;
    }

    [JSInvokable]
    public async Task OnViewportResizedAsync(int width, int height, double panX, double panY, double zoom)
    {
        if (width <= 50 || height <= 50)
            return;

        _canvasWidth = width;
        _canvasHeight = height;
        _panX = panX;
        _panY = panY;
        _zoom = zoom;

        if (!_initialMobileFitDone && _canvasWidth < 768 && Manager.Gates.Count > 0)
        {
            _initialMobileFitDone = true;
            FitCircuit();
            await SyncViewportOnlyAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnViewportChangedAsync(double panX, double panY, double zoom)
    {
        _panX = panX;
        _panY = panY;
        _zoom = Math.Clamp(zoom, 0.2, 2.5);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnGateMovedAsync(string gateId, double newX, double newY)
    {
        if (Manager == null) return;
        var gate = Manager.Gates.FirstOrDefault(g => g.Id == gateId);
        if (gate == null) return;

        gate.X = SnapToGrid(newX);
        gate.Y = SnapToGrid(newY);
        _selectedGate = gate;
        _selectedWire = null;
        _presentationGeometryHash = null;

        SetStatus($"Moved {gate.GateType} to ({gate.X:0}, {gate.Y:0}).", false);
        await NotifyUserModificationAsync(geometryChanged: true);
    }

    [JSInvokable]
    public async Task OnGateClickedAsync(string gateId)
    {
        if (Manager == null) return;
        var gate = Manager.Gates.FirstOrDefault(g => g.Id == gateId);
        if (gate == null) return;

        _selectedGate = gate;
        _selectedWire = null;

        if (gate is Switch sw)
        {
            sw.Toggle();
            SetStatus($"Switch changed to {(sw.State ? "ON (1)" : "OFF (0)")}.", false);
            await NotifyUserModificationAsync(geometryChanged: false);
        }
        else
        {
            SetStatus($"Selected {gate.GateType}.", false);
            await RequestSignalSyncAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    [JSInvokable]
    public async Task OnWireSelectedAsync(int wireIndex)
    {
        GetPresentationScene();
        if (wireIndex >= 0 && wireIndex < _presentationRoutes.Count)
        {
            _selectedWire = _presentationRoutes[wireIndex].Wire;
            _selectedGate = null;
            SetStatus("Wire selected. Use Delete selected to remove it.", false);
            await RequestSignalSyncAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    [JSInvokable]
    public async Task OnSelectionClearedAsync()
    {
        _selectedGate = null;
        _selectedWire = null;
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnWiringStartedAsync(string sourceGateId)
    {
        if (Manager == null) return Task.CompletedTask;
        var source = Manager.Gates.FirstOrDefault(g => g.Id == sourceGateId);
        if (source != null)
        {
            _selectedGate = source;
            _selectedWire = null;
            _validationMessage = null;
            SetStatus($"Connecting from {source.GateType}. Drag to a target input pin.", false);
            return InvokeAsync(StateHasChanged);
        }
        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task OnWireConnectionAttemptAsync(string sourceGateId, string targetGateId, int targetInputIndex)
    {
        if (Manager == null) return;

        var source = Manager.Gates.FirstOrDefault(g => g.Id == sourceGateId);
        var target = Manager.Gates.FirstOrDefault(g => g.Id == targetGateId);

        if (source == null || target == null)
        {
            SetStatus("No connection was made. Drag to a valid input pin.", false);
            await RefreshAsync();
            return;
        }

        _presentationGeometryHash = null;
        await CompleteWireConnectionAsync(source, target, targetInputIndex);
    }

    [JSInvokable]
    public async Task OnWireCancelledAsync()
    {
        SetStatus("No connection was made. Drag to a valid input pin.", false);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnRightClickInputPinAsync(string gateId, int inputIndex)
    {
        if (Manager == null) return;
        var pinGate = Manager.Gates.FirstOrDefault(g => g.Id == gateId);
        if (pinGate == null) return;

        var connected = Manager.Wires.FirstOrDefault(w =>
            ReferenceEquals(w.TargetGate, pinGate) &&
            w.TargetInputIndex == inputIndex);

        if (connected != null)
        {
            RemoveWireSafely(connected);
            _selectedWire = null;
            _presentationGeometryHash = null;
            SetStatus($"Disconnected the wire from input {inputIndex}.", false);
            await NotifyUserModificationAsync(geometryChanged: true);
        }
    }

    [JSInvokable]
    public async Task OnRightClickGateAsync(string gateId)
    {
        if (Manager == null) return;
        var gateToDelete = Manager.Gates.FirstOrDefault(g => g.Id == gateId);
        if (gateToDelete == null) return;

        string type = gateToDelete.GateType;
        RemoveGateSafely(gateToDelete);
        _presentationGeometryHash = null;

        SetStatus($"Deleted gate {type}.", false);
        await NotifyUserModificationAsync(geometryChanged: true);
    }

    [JSInvokable]
    public async Task OnRightClickWireAsync(int wireIndex)
    {
        GetPresentationScene();
        if (wireIndex >= 0 && wireIndex < _presentationRoutes.Count)
        {
            var wire = _presentationRoutes[wireIndex].Wire;
            if (Manager.Wires.Contains(wire))
            {
                RemoveWireSafely(wire);
                _selectedWire = null;
                _presentationGeometryHash = null;
                SetStatus("Wire deleted.", false);
                await NotifyUserModificationAsync(geometryChanged: true);
            }
        }
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
            await SyncViewportOnlyAsync();
        }
    }

    private async Task SlideVerticalAsync(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            _panY = -value * _zoom;
            await SyncViewportOnlyAsync();
        }
    }

    public sealed class CanvasScenePayload
    {
        [JsonPropertyName("panX")] public double PanX { get; set; }
        [JsonPropertyName("panY")] public double PanY { get; set; }
        [JsonPropertyName("zoom")] public double Zoom { get; set; }
        [JsonPropertyName("isPanMode")] public bool IsPanMode { get; set; }
        [JsonPropertyName("selectedGateId")] public string? SelectedGateId { get; set; }
        [JsonPropertyName("selectedWireIndex")] public int SelectedWireIndex { get; set; } = -1;
        [JsonPropertyName("gates")] public GateRenderDto[] Gates { get; set; } = Array.Empty<GateRenderDto>();
        [JsonPropertyName("wires")] public WireRenderDto[] Wires { get; set; } = Array.Empty<WireRenderDto>();
    }

    public sealed class SignalDeltaPayload
    {
        [JsonPropertyName("selectedGateId")] public string? SelectedGateId { get; set; }
        [JsonPropertyName("selectedWireIndex")] public int SelectedWireIndex { get; set; } = -1;
        [JsonPropertyName("gates")] public GateSignalDto[] Gates { get; set; } = Array.Empty<GateSignalDto>();
        [JsonPropertyName("wireHighs")] public bool[] WireHighs { get; set; } = Array.Empty<bool>();
    }

    public sealed class GateSignalDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("inputs")] public bool[] Inputs { get; set; } = Array.Empty<bool>();
        [JsonPropertyName("output")] public bool Output { get; set; }
    }

    public sealed class GateRenderDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("inputs")] public bool[] Inputs { get; set; } = Array.Empty<bool>();
        [JsonPropertyName("output")] public bool Output { get; set; }
    }

    public sealed class WireRenderDto
    {
        [JsonPropertyName("sourceId")] public string SourceId { get; set; } = string.Empty;
        [JsonPropertyName("targetId")] public string TargetId { get; set; } = string.Empty;
        [JsonPropertyName("targetInput")] public int TargetInput { get; set; }
        [JsonPropertyName("high")] public bool High { get; set; }
        [JsonPropertyName("points")] public double[] Points { get; set; } = Array.Empty<double>();
    }
}