using ScottPlot;
using System.Windows.Controls;

namespace VdlParser;

public partial class Graph : UserControl
{
    public Graph()
    {
        InitializeComponent();

        chart.Plot.Style(
            figureBackground: System.Drawing.Color.Gray,
            dataBackground: System.Drawing.Color.AliceBlue);
        chart.Plot.Margins(x: .1, y: .1);
        chart.Plot.ManualDataArea(new PixelPadding(1, 1, 1, 1));

        chart.Plot.Grid(false);
        chart.Plot.Frameless(false);

        chart.Plot.XAxis.SetSizeLimit(0, 0, 0);
        chart.Plot.XAxis.Line(false);
        chart.Plot.XAxis.Ticks(false);
        chart.Plot.YAxis.SetSizeLimit(0, 0, 0);
        chart.Plot.YAxis.Line(false);
        chart.Plot.YAxis.Ticks(false);

        chart.Plot.XAxis2.Hide();
        chart.Plot.YAxis2.Hide();

        chart.Refresh();
    }

    public void Reset()
    {
        chart.Plot.Clear();
        chart.Plot.AxisAuto();
        chart.Render();
    }

    public void AddVLine(double X, System.Drawing.Color color)
    {
        chart.Plot.AddVerticalLine(X, color, width: 1);
        chart.Render();
    }

    public void AddCurve(Sample[] samples, System.Drawing.Color color)
    {
        var x = samples.Select(s => (double)s.Timestamp);
        var y = samples.Select(s => s.Value);

        chart.Plot.AddScatter(x.ToArray(), y.ToArray(), color, lineWidth: 2, markerShape: MarkerShape.none);

        chart.Plot.AxisAuto();
        chart.Render();
    }


    // Internal 

    private class MeasureModel
    {
        public int ID { get; set; }
        public double Value { get; set; }
    }
}
