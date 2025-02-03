using System.Collections.ObjectModel;
using System.ComponentModel;
using MathNet.Numerics.Statistics;

namespace VdlParser;

[TypeConverter(typeof(FriendlyEnumConverter))]
public enum HandDataSource
{
    IndexFinger,
    MiddleFinger
}

[TypeConverter(typeof(FriendlyEnumConverter))]
public enum GazeDataSource
{
    YawRotation,
    PitchRotation
}

public enum ControllerState
{
    Empty,
    DataDisplayed,
    DataProcessed
}

public class Controller : IDisposable
{
    public ObservableCollection<Vdl> Vdls { get; }
    public PeakDetector HandPeakDetector { get; } = PeakDetector.Load(DataSourceType.Hand);
    public PeakDetector GazePeakDetector { get; } = PeakDetector.Load(DataSourceType.Gaze);
    public BlinkDetector BlinkDetector { get; } = BlinkDetector.Load();
    public double QuantileLow { get; set; } = 0.1;
    public double QuantileHigh { get; set; } = 0.9;

    public Settings Settings { get; } = Settings.Instance;

    public ControllerState State { get; set; } = ControllerState.Empty;

    public Controller()
    {
        Vdls = new ObservableCollection<Vdl>(_vdls);
    }

    public void Reset(Graph graph)
    {
        graph.Reset();
        State = ControllerState.Empty;
    }

    public void Add(Vdl vdl)
    {
        Vdls.Add(vdl);
    }

    public void Display(Vdl vdl, Graph graph)
    {
        var (handSamples, gazeSamples) = GetHandGazeSamples(vdl);

        graph.Reset();
        graph.AddCurve(handSamples, COLOR_HAND, "Hand");
        graph.AddCurve(gazeSamples, COLOR_GAZE, "Gaze");
        graph.Render();

        State = ControllerState.DataDisplayed;
    }

    public string AnalyzeAndDraw(Vdl vdl, Graph graph)
    {
        var (handSamples, gazeSamples) = GetHandGazeSamples(vdl);

        var handPeaks = HandPeakDetector.Find(handSamples);
        var gazePeaks = GazePeakDetector.Find(gazeSamples);

        var trials = Trial.GetTrials(vdl.Records, handPeaks, gazePeaks, Settings.MaxHandGazeDelay, Settings.TimestampSource);

        var gazeMisses = BlinkDetector.Find(gazeSamples);

        var pupilSizes = GetPupilSizes(vdl);

        var nbackTaskEvents = GetNBackTaskEvents(vdl);

        State = ControllerState.DataProcessed;

        // Draw

        graph.Reset();

        var labels = new HashSet<string>();
        string? EnsureSingle(string? label)
        {
            if (label != null && !labels.Add(label))
                return null;
            return label;
        }

        foreach (var blink in gazeMisses.Where(gm => gm.IsBlink))
        {
            if (Settings.BlinkShape == BlinkShape.Strip)
                graph.Plot.AddHorizontalSpan(blink.TimestampStart, blink.TimestampEnd, COLOR_BLINK, label: EnsureSingle("Blink"));
            else if (Settings.BlinkShape == BlinkShape.Ellipse)
                graph.Plot.AddEllipse((blink.TimestampStart + blink.TimestampEnd) / 2, 0,
                    blink.Duration / 2, 2, COLOR_BLINK_ELLIPSE);
        }

        graph.AddCurve(handSamples, COLOR_HAND, "Hand");
        graph.AddCurve(gazeSamples, COLOR_GAZE, "Gaze");

        foreach (var peak in handPeaks)
        {
            bool isMatched = trials.Any(trial => peak == trial.HandPeak);
            graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_HAND, isMatched ? 1 : 2, label: EnsureSingle("Hand peak start"));
        }

        foreach (var peak in gazePeaks)
        {
            bool isMatched = trials.Any(trial => peak == trial.GazePeak);
            graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_GAZE, isMatched ? 1 : 2, label: EnsureSingle("Gaze peak start"));
        }

        var markerY = handSamples.Max(sample => sample.Value) + 5;
        foreach (var (ts, nbte) in nbackTaskEvents)
        {
            graph.Plot.AddMarker(ts, 60, size: 12, color: NBackTaskEventColor(nbte.Type), label: EnsureSingle(NBackTaskEventLabel(nbte.Type)));
        }

        graph.Render();

        // Statistics

        var matchesCountPercentage = handPeaks.Length > 0 ? 100 * trials.Length / handPeaks.Length : 0;
        var responseIntervals = trials.Where(trial => trial.TimestampResponse > 0).Select(trial => (double)(trial.TimestampStart - trial.TimestampResponse));
        var (responseIntervalMean, responseIntervalStd) = responseIntervals.MeanStandardDeviation();
        var gazeHandIntervals = trials.Select(trial => (double)trial.GazeHandInterval);
        var (gazeHandIntervalMean, gazeHandIntervalStd) = gazeHandIntervals.MeanStandardDeviation();
        var glanceDurations = gazePeaks.Select(peak => (double)(peak.TimestampEnd - peak.TimestampStart));
        var (glanceDurationMean, glanceDurationStd) = glanceDurations.MeanStandardDeviation();
        var (pupilSizeMean, pupilSizeStd) = pupilSizes.MeanStandardDeviation();
        var longEyeLostCount = gazeMisses.Where(gm => gm.Duration > BlinkDetector.BlinkMaxDuration).Count();

        return string.Join('\n', [
            $"Hand/Gaze peaks: {handPeaks.Length}/{gazePeaks.Length}",
            $"  match count = {trials.Length} ({matchesCountPercentage:F1}%)",
            $"Response delay",
            $"  mean = {responseIntervalMean:F0} ms (SD = {responseIntervalStd:F1} ms)",
            $"  median = {responseIntervals.Median():F0} ms ({responseIntervals.Quantile(QuantileLow):F0}..{responseIntervals.Quantile(QuantileHigh):F0} ms)",
            $"Gaze delay",
            $"  mean = {gazeHandIntervalMean:F0} ms (SD = {gazeHandIntervalStd:F1} ms)",
            $"  median = {gazeHandIntervals.Median():F0} ms ({gazeHandIntervals.Quantile(QuantileLow):F0}..{gazeHandIntervals.Quantile(QuantileHigh):F0} ms)",
            $"Glance duration:",
            $"  mean = {glanceDurationMean:F0} ms (SD = {glanceDurationStd:F0} ms)",
            $"  median = {glanceDurations.Median():F0} ms ({glanceDurations.Quantile(QuantileLow):F0}..{glanceDurations.Quantile(QuantileHigh):F0} ms)",
            $"Pupil size",
            $"  mean = {pupilSizeMean:F2} (SD = {pupilSizeStd:F2})",
            $"  median = {pupilSizes.Median():F2} ({pupilSizes.Quantile(QuantileLow):F2}..{pupilSizes.Quantile(QuantileHigh):F2})",
            $"Gaze-lost events: {gazeMisses.Length}",
            $"  blinks: {gazeMisses.Where(gm => gm.IsBlink).Count()}",
            $"  eyes closed or lost: {longEyeLostCount}",
        ]);
    }

    public void Dispose()
    {
        PeakDetector.Save(DataSourceType.Hand, HandPeakDetector);
        PeakDetector.Save(DataSourceType.Gaze, GazePeakDetector);
        BlinkDetector.Save(BlinkDetector);

        GC.SuppressFinalize(this);
    }

    // Internal

    readonly System.Drawing.Color COLOR_HAND = System.Drawing.Color.Blue;
    readonly System.Drawing.Color COLOR_GAZE = System.Drawing.Color.Red;
    readonly System.Drawing.Color COLOR_BLINK = System.Drawing.Color.LightGray;
    readonly System.Drawing.Color COLOR_BLINK_ELLIPSE = System.Drawing.Color.Gray;

    List<Vdl> _vdls = [];

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

    private long GetTimestamp(Record record) => Settings.TimestampSource switch
    {
        TimestampSource.Headset => record.TimestampHeadset,
        TimestampSource.System => record.TimestampSystem,
        _ => throw new NotSupportedException($"{Settings.TimestampSource} timestamp source is not supported"),
    };

    private (Sample[], Sample[]) GetHandGazeSamples(Vdl vdl) 
    {
        return (
            Settings.HandDataSource switch
            {
                HandDataSource.IndexFinger => vdl.Records.Select(record => new Sample(GetTimestamp(record), record.HandIndex.Y)).ToArray(),
                HandDataSource.MiddleFinger => vdl.Records.Select(record => new Sample(GetTimestamp(record), record.HandMiddle.Y)).ToArray(),
                _ => throw new NotImplementedException($"{Settings.HandDataSource} hand data source is not yet supported")
            },
            Settings.GazeDataSource switch
            {
                GazeDataSource.YawRotation => vdl.Records.Select(record => new Sample(GetTimestamp(record), record.Eye.Yaw)).ToArray(),
                GazeDataSource.PitchRotation => vdl.Records.Select(record => new Sample(GetTimestamp(record), record.Eye.Pitch)).ToArray(),
                _ => throw new NotImplementedException($"{Settings.GazeDataSource} gaze data source is not yet supported")
            }
        );
    }

    private IEnumerable<(long, NBackTaskEvent)> GetNBackTaskEvents(Vdl vdl) =>
        vdl.Records.Where(r => r.NBackTaskEvent != null).Select(record => (GetTimestamp(record), record.NBackTaskEvent!));

    private IEnumerable<double> GetPupilSizes(Vdl vdl) =>
        vdl.Records
            .SkipWhile(record => record.NBackTaskEvent?.Type != NBackTaskEventType.SessionStart)
            .TakeWhile(record => record.NBackTaskEvent?.Type != NBackTaskEventType.SessionEnd)
            .Where(record => record.LeftPupil.Openness > 0.6 && record.RightPupil.Openness > 0.6)
            .Select(record => (record.LeftPupil.Size + record.RightPupil.Size) / 2);
}
