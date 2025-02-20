using VdlParser.Detectors;
using VdlParser.Models;

namespace VdlParser;

public enum GraphContent
{
    Empty,
    RawData,
    Processed
}

public enum GraphCurve
{
    Gaze,
    Hand,
    PupilSize,
    PupilOpenness
}

public class GraphRenderer(Controls.Graph graph, GraphSettings graphSettings)
{
    public GraphContent Content { get; set; } = GraphContent.Empty;

    public void Reset()
    {
        _graph.Reset();
        Content = GraphContent.Empty;
    }

    public void Render()
    {
        _graph.Render();
    }

    public void AddRawData(Sample[] samples, GraphCurve curve)
    {
        var (color, label) = GetCurveProps(curve);
        if (curve == GraphCurve.Gaze || curve == GraphCurve.Hand)
            _graph.AddCurve(samples, color, label);
        else
            _graph.AddCurve2(samples, color, label);

        if (Content != GraphContent.Processed)
        {
            Content = GraphContent.RawData;
        }
    }

    public void AddRawData(Processor processor)
    {
        AddRawData(processor.HandSamples, GraphCurve.Hand);
        AddRawData(processor.GazeSamples, GraphCurve.Gaze);
        if (_graphSettings.HasPupilSize)
            AddRawData(processor.PupilSizeSamples, GraphCurve.PupilSize);
        if (_graphSettings.HasPupilOpenness)
            AddRawData(processor.PupilOpennessSamples, GraphCurve.PupilOpenness);
    }

    public void DisplayProcessedData(Processor processor)
    {
        _graph.Reset();

        var labels = new HashSet<string>();
        string? EnsureSingle(string? label)
        {
            if (label != null && !labels.Add(label))
                return null;
            return label;
        }

        foreach (var blink in processor.Blinks)
        {
            _graph.Plot.AddHorizontalSpan(blink.StartTimestamp, blink.EndTimestamp, COLOR_BLINK, label: EnsureSingle("Blink"));
        }

        AddRawData(processor);

        foreach (var peak in processor.HandPeaks)
        {
            bool isMatched = processor.Trials.Any(trial => peak == trial.HandPeak && trial.HasHandGazeMatch);
            _graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_HAND, isMatched ? 1 : 2,
                ScottPlot.LineStyle.Dot, label: EnsureSingle("Hand peak start"));
        }

        foreach (var peak in processor.GazePeaks)
        {
            bool isMatched = processor.Trials.Any(trial => peak == trial.GazePeak && trial.HasHandGazeMatch);
            _graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_GAZE, isMatched ? 1 : 2,
                ScottPlot.LineStyle.Dot, label: EnsureSingle("Gaze peak start"));
        }

        var markerY = processor.HandSamples.Max(sample => sample.Value) + 5;
        foreach (var (ts, nbte) in processor.NBackTaskEvents)
        {
            _graph.Plot.AddMarker(ts, markerY, size: 12, color: NBackTaskEventColor(nbte.Type),
                label: EnsureSingle(NBackTaskEventLabel(nbte.Type)));
        }

        _graph.Render();

        Content = GraphContent.Processed;
    }

    // Internal

    readonly System.Drawing.Color COLOR_HAND = System.Drawing.Color.Blue;
    readonly System.Drawing.Color COLOR_GAZE = System.Drawing.Color.Red;
    readonly System.Drawing.Color COLOR_PUPIL_SIZE = System.Drawing.Color.Purple;
    readonly System.Drawing.Color COLOR_PUPIL_OPENNESS = System.Drawing.Color.MediumPurple;
    readonly System.Drawing.Color COLOR_BLINK = System.Drawing.Color.LightGray;

    readonly Controls.Graph _graph = graph;
    readonly GraphSettings _graphSettings = graphSettings;

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
