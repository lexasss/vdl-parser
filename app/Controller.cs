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

    public Settings Settings { get; } = Settings.Instance;

    public ControllerState State { get; set; } = ControllerState.Empty;

    public Controller()
    {
        Vdls = new ObservableCollection<Vdl>(_vdls);
    }

    public void Reset(Graph plot)
    {
        plot.Reset();
        State = ControllerState.Empty;
    }

    public void Add(Vdl vdl)
    {
        Vdls.Add(vdl);
    }

    public void Display(Vdl vdl, Graph graph)
    {
        var (handDatapoint, gazeDatapoint) = GetTimeseries(vdl);

        graph.Reset();
        graph.AddCurve(handDatapoint, COLOR_HAND, "Hand");
        graph.AddCurve(gazeDatapoint, COLOR_GAZE, "Gaze");
        graph.Render();

        State = ControllerState.DataDisplayed;
    }

    public string AnalyzeAndDraw(Vdl vdl, Graph graph)
    {
        var (handDatapoints, gazeDatapoints) = GetTimeseries(vdl);

        var handPeaks = HandPeakDetector.Find(handDatapoints);
        var gazePeaks = GazePeakDetector.Find(gazeDatapoints);

        var matches = MatchPeaks(handPeaks, gazePeaks);

        var gazeMisses = BlinkDetector.Find(gazeDatapoints);

        var nbackTaskEvents = GetNBackTaskEvents(vdl);
        var (pupilSize, pupilSizeStd) = EstimatePupilSize(vdl);

        State = ControllerState.DataProcessed;

        // Draw

        graph.Reset();

        var labels = new HashSet<string>();

        foreach (var blink in gazeMisses.Where(gm => gm.IsBlink))
        {
            string? label = "Blink";

            if (labels.Contains(label))
                label = null;
            else
                labels.Add(label);

            if (Settings.BlinkShape == BlinkShape.Strip)
                graph.Plot.AddHorizontalSpan(blink.TimestampStart, blink.TimestampEnd, COLOR_BLINK, label: label);
            else if (Settings.BlinkShape == BlinkShape.Ellipse)
                graph.Plot.AddEllipse((blink.TimestampStart + blink.TimestampEnd) / 2, 0,
                    blink.Duration / 2, 2, COLOR_BLINK_ELLIPSE);
        }

        graph.AddCurve(handDatapoints, COLOR_HAND, "Hand");
        graph.AddCurve(gazeDatapoints, COLOR_GAZE, "Gaze");

        foreach (var peak in handPeaks)
        {
            string? label = "Hand peak start";

            if (labels.Contains(label))
                label = null;
            else
                labels.Add(label);

            bool isMatched = matches.Any((pair) => peak == pair.Item1);
            graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_HAND, isMatched ? 1 : 2, label: label);
        }

        foreach (var peak in gazePeaks)
        {
            string? label = "Gaze peak start";

            if (labels.Contains(label))
                label = null;
            else
                labels.Add(label);

            bool isMatched = matches.Any((pair) => peak == pair.Item2);
            graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_GAZE, isMatched ? 1 : 2, label: label);
        }

        foreach (var (ts, nbte) in nbackTaskEvents)
        {
            var color = nbte.Type switch
            {
                NBackTaskEventType.SessionStart or NBackTaskEventType.SessionEnd => System.Drawing.Color.Green,
                NBackTaskEventType.TrialStart => System.Drawing.Color.Purple,
                NBackTaskEventType.TrialResponse => System.Drawing.Color.Orange,
                NBackTaskEventType.TrialEnd => System.Drawing.Color.Blue,
                _ => System.Drawing.Color.Black
            };
            string? label = nbte.Type switch
            {
                NBackTaskEventType.SessionStart or NBackTaskEventType.SessionEnd => "Session start/end",
                NBackTaskEventType.TrialStart => "Trial start",
                NBackTaskEventType.TrialResponse => "Response",
                NBackTaskEventType.TrialEnd => "Trial end",
                _ => null
            };
            if (label != null)
            {
                if (labels.Contains(label))
                    label = null;
                else
                    labels.Add(label);
            }
            graph.Plot.AddMarker(ts, 60, size: 12, color: color, label: label);
        }

        graph.Render();

        // Statistics

        var gazeHandIntervals = matches.Select(pair => (double)(pair.Item2.TimestampStart - pair.Item1.TimestampStart));
        var matchesCountPercentage = handPeaks.Length > 0 ? 100 * matches.Length / handPeaks.Length : 0;

        return string.Join('\n', [
            $"Sample count: {vdl.RecordCount}",
            $"Hand peak count: {handPeaks.Length}",
            $"Gaze peak count: {gazePeaks.Length}",
            $"Matches:",
            $"  count = {matches.Length}/{handPeaks.Length} ({matchesCountPercentage:F1}%)",
            $"  gaze delay = {gazeHandIntervals.Median():F0} ms (SD = {gazeHandIntervals.StandardDeviation():F1} ms)",
            $"Gaze-lost event count: {gazeMisses.Length}",
            $"  blinks: {gazeMisses.Where(gm => gm.IsBlink).Count()}",
            $"  eyes closed or lost: {gazeMisses.Where(gm => gm.Duration > BlinkDetector.BlinkMaxDuration).Count()}",
            $"Pupil size: {pupilSize:F2} (SD = {pupilSizeStd:F2})",
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

    private long GetTimestamp(Record record) => Settings.TimestampSource switch
    {
        TimestampSource.Headset => record.TimestampHeadset,
        TimestampSource.System => record.TimestampSystem,
        _ => throw new NotSupportedException($"{Settings.TimestampSource} timestamp source is not supported"),
    };

    private (Sample[], Sample[]) GetTimeseries(Vdl vdl) 
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

    private (Peak, Peak)[] MatchPeaks(Peak[] handPeaks, Peak[] gazePeaks)
    {
        var result = new List<(Peak, Peak)>();

        int gazeIndex = 0;
        foreach (Peak handPeak in handPeaks)
        {
            while (gazeIndex < gazePeaks.Length)
            {
                var gazePeak = gazePeaks[gazeIndex++];
                if (Math.Abs(gazePeak.TimestampStart - handPeak.TimestampStart) < Settings.MaxHandGazeDelay)
                {
                    result.Add((handPeak, gazePeak));
                    break;
                }
                else if (gazePeak.TimestampStart > handPeak.TimestampStart)
                {
                    gazeIndex -= 1;
                    break;
                }
            }
        }

        return result.ToArray();
    }

    private (double, double) EstimatePupilSize(Vdl vdl) =>
        vdl.Records
            .Where(record => record.LeftPupil.Openness > 0.6 && record.RightPupil.Openness > 0.6)
            .Select(record => (record.LeftPupil.Size + record.RightPupil.Size) / 2)
            .MeanStandardDeviation();
}
