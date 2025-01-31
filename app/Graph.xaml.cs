using ScottPlot;
using ScottPlot.Styles;
using System.Windows.Controls;

namespace VdlParser;

public partial class Graph : UserControl
{
    public Plot Plot => chart.Plot;

    public Graph()
    {
        InitializeComponent();

        chart.Plot.Style(dataBackground: System.Drawing.Color.AliceBlue);
        chart.Plot.ManualDataArea(new PixelPadding(30, 0, 20, 0));

        chart.Plot.Grid(true);

        chart.Plot.XAxis.Line(false);
        chart.Plot.YAxis.Line(false);

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

    public void Render()
    {
        chart.Plot.AxisAuto();
        chart.Plot.Legend(true);
        chart.Render();
    }

    public void AddCurve(Sample[] samples, System.Drawing.Color color, string label)
    {
        var x = samples.Select(s => (double)s.Timestamp);
        var y = samples.Select(s => s.Value);

        chart.Plot.AddScatter(x.ToArray(), y.ToArray(), color, lineWidth: 2, markerShape: MarkerShape.none, label: label);
    }
}
