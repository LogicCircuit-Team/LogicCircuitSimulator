using System;
using System.Threading.Tasks;
using LogicCore.Interfaces;

namespace BlazorApp1.Drawing
{
    public static class GateRenderer
    {
        public static async Task DrawAsync<TBatch>(TBatch batch, IGate gate)
        {
            dynamic drawing = batch;
            string type = gate.GateType.ToUpperInvariant();

            await drawing.StrokeStyleAsync("#222222");
            await drawing.LineWidthAsync(3);
            await drawing.FillStyleAsync("#222222");

            int inputCount = gate.Inputs?.Length ?? 2;

            switch (type)
            {
                case "AND":
                    await DrawAnd(drawing, gate.X, gate.Y, inputCount, false);
                    break;
                case "NAND":
                    await DrawAnd(drawing, gate.X, gate.Y, inputCount, true);
                    break;
                case "OR":
                    await DrawOr(drawing, gate.X, gate.Y, false);
                    break;
                case "NOR":
                    await DrawOr(drawing, gate.X, gate.Y, true);
                    break;
                case "XOR":
                    await DrawXor(drawing, gate.X, gate.Y);
                    break;
                case "NOT":
                    await DrawNot(drawing, gate.X, gate.Y);
                    break;
                case "SWITCH":
                    await DrawSwitch(drawing, gate.X, gate.Y, gate.Output);
                    break;
                case "LAMP":
                    await DrawLamp(drawing, gate.X, gate.Y, gate.Output);
                    break;
                default:
                    await DrawUnknown(drawing, gate.X, gate.Y, type);
                    break;
            }

            await drawing.StrokeStyleAsync("#222222");
            await drawing.FillStyleAsync("#222222");
        }

        private static async Task DrawAnd(dynamic d, double x, double y, int inputCount, bool inverted)
        {
            double height = Math.Max(60, inputCount * 20 + 20);

            await d.BeginPathAsync();
            await d.MoveToAsync(x, y);
            await d.LineToAsync(x + 35, y);
            await d.QuadraticCurveToAsync(x + 70, y, x + 70, y + height / 2);
            await d.QuadraticCurveToAsync(x + 70, y + height, x + 35, y + height);
            await d.LineToAsync(x, y + height);
            await d.ClosePathAsync();
            await d.StrokeAsync();

            double spacing = height / (inputCount + 1);
            for (int i = 0; i < inputCount; i++)
            {
                double inputY = y + spacing * (i + 1);
                await Line(d, x - 35, inputY, x, inputY);
            }

            double outY = y + height / 2;
            if (inverted)
            {
                await d.BeginPathAsync();
                await d.ArcAsync(x + 76, outY, 6, 0, Math.PI * 2);
                await d.StrokeAsync();
                await Line(d, x + 82, outY, x + 112, outY);
            }
            else
            {
                await Line(d, x + 70, outY, x + 105, outY);
            }

            await Label(d, inverted ? "NAND" : "AND", x + 8, y + height + 20);
        }

        private static async Task DrawOr(dynamic d, double x, double y, bool inverted)
        {
            await d.BeginPathAsync();
            await d.MoveToAsync(x, y);
            await d.QuadraticCurveToAsync(x + 22, y + 30, x, y + 60);
            await d.QuadraticCurveToAsync(x + 43, y + 55, x + 80, y + 30);
            await d.QuadraticCurveToAsync(x + 43, y + 5, x, y);
            await d.StrokeAsync();

            await Line(d, x - 35, y + 15, x + 9, y + 15);
            await Line(d, x - 35, y + 45, x + 9, y + 45);

            if (inverted)
            {
                await d.BeginPathAsync();
                await d.ArcAsync(x + 86, y + 30, 6, 0, Math.PI * 2);
                await d.StrokeAsync();
                await Line(d, x + 92, y + 30, x + 120, y + 30);
            }
            else
            {
                await Line(d, x + 80, y + 30, x + 112, y + 30);
            }

            await Label(d, inverted ? "NOR" : "OR", x + 12, y + 82);
        }

        private static async Task DrawXor(dynamic d, double x, double y)
        {
            await d.BeginPathAsync();
            await d.MoveToAsync(x - 8, y);
            await d.QuadraticCurveToAsync(x + 14, y + 30, x - 8, y + 60);
            await d.StrokeAsync();

            await DrawOr(d, x, y, false);
            await Label(d, "XOR", x + 12, y + 102);
        }

        private static async Task DrawNot(dynamic d, double x, double y)
        {
            await d.BeginPathAsync();
            await d.MoveToAsync(x, y);
            await d.LineToAsync(x, y + 60);
            await d.LineToAsync(x + 65, y + 30);
            await d.ClosePathAsync();
            await d.StrokeAsync();

            await d.BeginPathAsync();
            await d.ArcAsync(x + 71, y + 30, 6, 0, Math.PI * 2);
            await d.StrokeAsync();

            await Line(d, x - 35, y + 30, x, y + 30);
            await Line(d, x + 77, y + 30, x + 110, y + 30);

            await Label(d, "NOT", x + 12, y + 82);
        }

        private static async Task DrawSwitch(dynamic d, double x, double y, bool state)
        {
            await d.StrokeStyleAsync("#222222");
            await d.LineWidthAsync(3);
            await Line(d, x - 30, y + 17, x, y + 17);
            await d.BeginPathAsync();
            await d.RectAsync(x, y, 60, 35);
            await d.StrokeAsync();

            string color = state ? "#00AA44" : "#777777";
            await d.StrokeStyleAsync(color);
            await d.FillStyleAsync(color);
            await d.BeginPathAsync();
            await d.ArcAsync(x + 30, y + 17, 7, 0, Math.PI * 2);
            await d.FillAsync();
            await d.StrokeAsync();

            await Line(d, x + 60, y + 17, x + 90, y + 17);
            await Label(d, state ? "SWITCH (1)" : "SWITCH (0)", x - 10, y + 55);
        }

        private static async Task DrawLamp(dynamic d, double x, double y, bool state)
        {
            string color = state ? "#00AA44" : "#777777";
            await d.StrokeStyleAsync(color);
            await d.LineWidthAsync(3);
            await Line(d, x - 30, y + 25, x + 10, y + 25);

            await d.BeginPathAsync();
            await d.ArcAsync(x + 25, y + 25, 15, 0, Math.PI * 2);
            await d.StrokeAsync();

            await Line(d, x + 40, y + 25, x + 80, y + 25);
            await Label(d, state ? "LAMP (1)" : "LAMP (0)", x - 5, y + 60);
        }

        private static async Task DrawUnknown(dynamic d, double x, double y, string type)
        {
            await d.BeginPathAsync();
            await d.RectAsync(x, y, 80, 50);
            await d.StrokeAsync();
            await Label(d, type, x + 5, y + 75);
        }

        private static async Task Line(dynamic d, double x1, double y1, double x2, double y2)
        {
            await d.BeginPathAsync();
            await d.MoveToAsync(x1, y1);
            await d.LineToAsync(x2, y2);
            await d.StrokeAsync();
        }

        private static async Task Label(dynamic d, string text, double x, double y)
        {
            await d.FillStyleAsync("#222222");
            await d.FillTextAsync(text, x, y);
        }
    }
}