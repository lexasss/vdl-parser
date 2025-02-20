using ScottPlot;
using System.ComponentModel;
using System.Windows.Controls;
using VdlParser.Detectors;
using VdlParser.Models;

namespace VdlParser.Controls;

public enum GraphDisplayState
{
    Empty,
    RawData,
    ProcessedData
}

public enum GraphCurve
{
    Gaze,
    Hand,
    PupilSize,
    PupilOpenness
}

public partial class Graph : UserControl, INotifyPropertyChanged
{
    public GraphDisplayState DisplayState { get; set; } = GraphDisplayState.Empty;
    public GraphSettings Settings { get; } = Storage.Load<GraphSettings>();

    public bool IsLegendVisible
    {
        get => _isLegendVisible;
        set
        {
            _isLegendVisible = value;

            chart.Plot.Legend(_isLegendVisible);
            chart.Render();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLegendVisible)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Graph()
    {
        InitializeComponent();

        chart.Plot.Style(dataBackground: System.Drawing.Color.AliceBlue);
        chart.Plot.ManualDataArea(new PixelPadding(30, 30, 20, 0));

        chart.Plot.Grid(true);

        chart.Plot.XAxis.Line(false);
        chart.Plot.YAxis.Line(false);

        chart.Plot.XAxis2.Hide();

        chart.Plot.RightAxis.Line(false);
        chart.Plot.RightAxis.Ticks(true);
        chart.Plot.RightAxis.IsVisible = true;

        chart.Refresh();
    }

    public void Reset()
    {
        chart.Plot.Clear();
        chart.Plot.AxisAuto();
        chart.Render();

        DisplayState = GraphDisplayState.Empty;
    }

    public void Render()
    {
        chart.Plot.AxisAuto();
        chart.Plot.Legend(_isLegendVisible);
        chart.Render();
    }


    public void AddRawData(Sample[] samples, GraphCurve curve)
    {
        var (color, label) = GetCurveProps(curve);
        if (curve == GraphCurve.Gaze || curve == GraphCurve.Hand)
            AddCurve(samples, color, label);
        else
            AddCurve2(samples, color, label);

        if (DisplayState != GraphDisplayState.ProcessedData)
        {
            DisplayState = GraphDisplayState.RawData;
        }
    }

    public void AddRawData(Processor processor)
    {
        AddRawData(processor.HandSamples, GraphCurve.Hand);
        AddRawData(processor.GazeSamples, GraphCurve.Gaze);
        if (Settings.HasPupilSize)
            AddRawData(processor.PupilSizeSamples, GraphCurve.PupilSize);
        if (Settings.HasPupilOpenness)
            AddRawData(processor.PupilOpennessSamples, GraphCurve.PupilOpenness);
    }

    public void DisplayProcessedData(Processor processor)
    {
        Reset();

        var labels = new HashSet<string>();
        string? EnsureSingle(string? label)
        {
            if (label != null && !labels.Add(label))
                return null;
            return label;
        }

        foreach (var blink in processor.Blinks)
        {
            chart.Plot.AddHorizontalSpan(blink.StartTimestamp, blink.EndTimestamp, COLOR_BLINK, label: EnsureSingle("Blink"));
        }

        AddRawData(processor);

        foreach (var peak in processor.HandPeaks)
        {
            bool isMatched = processor.Trials.Any(trial => peak == trial.HandPeak && trial.HasHandGazeMatch);
            chart.Plot.AddVerticalLine(peak.TimestampStart, COLOR_HAND, isMatched ? 1 : 2,
                LineStyle.Dot, label: EnsureSingle("Hand peak start"));
        }

        foreach (var peak in processor.GazePeaks)
        {
            bool isMatched = processor.Trials.Any(trial => peak == trial.GazePeak && trial.HasHandGazeMatch);
            chart.Plot.AddVerticalLine(peak.TimestampStart, COLOR_GAZE, isMatched ? 1 : 2,
                LineStyle.Dot, label: EnsureSingle("Gaze peak start"));
        }

        var markerY = processor.HandSamples.Max(sample => sample.Value) + 5;
        foreach (var (ts, nbte) in processor.NBackTaskEvents)
        {
            chart.Plot.AddMarker(ts, markerY, size: 12, color: NBackTaskEventColor(nbte.Type),
                label: EnsureSingle(NBackTaskEventLabel(nbte.Type)));
        }

        Render();

        DisplayState = GraphDisplayState.ProcessedData;
    }

    // Internal

    readonly System.Drawing.Color COLOR_HAND = System.Drawing.Color.Blue;
    readonly System.Drawing.Color COLOR_GAZE = System.Drawing.Color.Red;
    readonly System.Drawing.Color COLOR_PUPIL_SIZE = System.Drawing.Color.Purple;
    readonly System.Drawing.Color COLOR_PUPIL_OPENNESS = System.Drawing.Color.MediumPurple;
    readonly System.Drawing.Color COLOR_BLINK = System.Drawing.Color.LightGray;

    bool _isLegendVisible = false;

    private void AddCurve(Sample[] samples, System.Drawing.Color color, string label)
    {
        var x = samples.Select(s => (double)s.Timestamp);
        var y = samples.Select(s => s.Value);

        chart.Plot.AddScatter(x.ToArray(), y.ToArray(), color,
            lineWidth: 2, markerShape: MarkerShape.none, label: label);
    }

    private void AddCurve2(Sample[] samples, System.Drawing.Color color, string label)
    {
        var x = samples.Select(s => (double)s.Timestamp);
        var y = samples.Select(s => s.Value);

        var scatter = chart.Plot.AddScatter(x.ToArray(), y.ToArray(), color,
            lineWidth: 2, markerShape: MarkerShape.none, label: label);
        scatter.YAxisIndex = chart.Plot.RightAxis.AxisIndex;
    }

    (System.Drawing.Color, string) GetCurveProps(GraphCurve curve) => curve switch
    {
        GraphCurve.Gaze => (COLOR_GAZE, "Gaze"),
        GraphCurve.Hand => (COLOR_HAND, "Hand"),
        GraphCurve.PupilSize => (COLOR_PUPIL_SIZE, "Pupil size"),
        GraphCurve.PupilOpenness => (COLOR_PUPIL_OPENNESS, "Pupil openness"),
        _ => (System.Drawing.Color.Black, "")
    };

    private System.Drawing.Color NBackTaskEventColor(NBackTaskEventType type) => type switch
    {
        NBackTaskEventType.SessionStart or NBackTaskEventType.SessionEnd => System.Drawing.Color.Green,
        NBackTaskEventType.TrialStart => System.Drawing.Color.Purple,
        NBackTaskEventType.TrialResponse => System.Drawing.Color.Orange,
        NBackTaskEventType.TrialEnd => System.Drawing.Color.Blue,
        _ => System.Drawing.Color.Black
    };

    private string? NBackTaskEventLabel(NBackTaskEventType type) => type switch
    {
        NBackTaskEventType.SessionStart or NBackTaskEventType.SessionEnd => "Session start/end",
        NBackTaskEventType.TrialStart => "Trial start",
        NBackTaskEventType.TrialResponse => "Response",
        NBackTaskEventType.TrialEnd => "Trial end",
        _ => null
    };
}
