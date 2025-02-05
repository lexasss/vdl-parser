namespace VdlParser;

public enum GraphContent
{
    Empty,
    RawData,
    Processed
}

public class GraphRenderer(Graph graph)
{
    public GraphContent Content { get; set; } = GraphContent.Empty;

    public void Reset()
    {
        _graph.Reset();
        Content = GraphContent.Empty;
    }

    public void DisplayRawData(Sample[] handSamples, Sample[] gazeSamples)
    {
        _graph.Reset();
        _graph.AddCurve(handSamples, COLOR_HAND, "Hand");
        _graph.AddCurve(gazeSamples, COLOR_GAZE, "Gaze");
        _graph.Render();

        Content = GraphContent.RawData;
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

        foreach (var blink in processor.GazeDataMisses.Where(gm => gm.IsBlink))
        {
            if (_settings.BlinkShape == BlinkShape.Strip)
                _graph.Plot.AddHorizontalSpan(blink.TimestampStart, blink.TimestampEnd, COLOR_BLINK, label: EnsureSingle("Blink"));
            else if (_settings.BlinkShape == BlinkShape.Ellipse)
                _graph.Plot.AddEllipse((blink.TimestampStart + blink.TimestampEnd) / 2, 0,
                    blink.Duration / 2, 2, COLOR_BLINK_ELLIPSE);
        }

        _graph.AddCurve(processor.HandSamples, COLOR_HAND, "Hand");
        _graph.AddCurve(processor.GazeSamples, COLOR_GAZE, "Gaze");

        foreach (var peak in processor.HandPeaks)
        {
            bool isMatched = processor.Trials.Any(trial => peak == trial.HandPeak);
            _graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_HAND, isMatched ? 1 : 2,
                ScottPlot.LineStyle.Dot, label: EnsureSingle("Hand peak start"));
        }

        foreach (var peak in processor.GazePeaks)
        {
            bool isMatched = processor.Trials.Any(trial => peak == trial.GazePeak);
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
    readonly System.Drawing.Color COLOR_BLINK = System.Drawing.Color.LightGray;
    readonly System.Drawing.Color COLOR_BLINK_ELLIPSE = System.Drawing.Color.Gray;

    readonly Settings _settings = Settings.Instance;

    readonly Graph _graph = graph;

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
