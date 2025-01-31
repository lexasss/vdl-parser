using System.Collections.ObjectModel;
using MathNet.Numerics;
using MathNet.Numerics.Statistics;

namespace VdlParser;

public enum Finger
{
    Index,
    Middle
}

public enum GazeRotation
{
    Yaw,
    Pitch
}

public enum ControllerState
{
    Empty,
    RawDataDisplayed,
    PeaksDetected
}

public class Controller : IDisposable
{
    public ObservableCollection<Vdl> Vdls { get; }
    public PeakDetector HandPeakDetector { get; } = PeakDetector.Load(DataSourceType.Finger);
    public PeakDetector GazePeakDetector { get; } = PeakDetector.Load(DataSourceType.Eye);
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

    public void Display(Vdl vdl, Graph plot)
    {
        var (fingerDatapoint, gazeDatapoint) = GetTimeseries(vdl);

        plot.Reset();
        plot.AddCurve(fingerDatapoint, COLOR_FINGER);
        plot.AddCurve(gazeDatapoint, COLOR_GAZE);
        plot.Render();

        State = ControllerState.RawDataDisplayed;
    }

    public string AnalyzeAndDraw(Vdl vdl, Graph graph)
    {
        var (fingerDatapoints, gazeDatapoints) = GetTimeseries(vdl);

        var fingerPeaks = HandPeakDetector.Find(fingerDatapoints);
        var gazePeaks = GazePeakDetector.Find(gazeDatapoints);

        var matches = MatchPeaks(fingerPeaks, gazePeaks);

        var gazeMisses = BlinkDetector.Find(gazeDatapoints);

        // Draw
        graph.Reset();
        graph.AddCurve(fingerDatapoints, COLOR_FINGER);
        graph.AddCurve(gazeDatapoints, COLOR_GAZE);

        foreach (var peak in fingerPeaks)
        {
            bool isMatched = matches.Any((pair) => peak == pair.Item1);
            graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_FINGER, isMatched ? 1 : 2);
        }

        foreach (var peak in gazePeaks)
        {
            bool isMatched = matches.Any((pair) => peak == pair.Item2);
            graph.Plot.AddVerticalLine(peak.TimestampStart, COLOR_GAZE, isMatched ? 1 : 2);
        }

        foreach (var blink in gazeMisses.Where(gm => gm.IsBlink))
        {
            graph.Plot.AddEllipse((blink.TimestampStart + blink.TimestampEnd) / 2, 0,
                blink.Duration / 2, 2, COLOR_BLINK);
        }

        graph.Render();

        State = ControllerState.PeaksDetected;

        var gazeHandIntervals = matches.Select(pair => (double)(pair.Item2.TimestampStart - pair.Item1.TimestampStart));

        return string.Join('\n', [
            $"Sample count: {vdl.RecordCount}",
            $"Hand peak count: {fingerPeaks.Length}",
            $"Gaze peak count: {gazePeaks.Length}",
            $"Matches:",
            $"  count = {matches.Length}/{fingerPeaks.Length} ({100*matches.Length/fingerPeaks.Length:F1}%)",
            $"  gaze delay = {gazeHandIntervals.Median():F0} ms (SD = {gazeHandIntervals.StandardDeviation():F1} ms)",
            $"Gaze-lost event count: {gazeMisses.Length}",
            $"  blinks: {gazeMisses.Where(gm => gm.IsBlink).Count()}",
            $"  eyes closed or lost: {gazeMisses.Where(gm => gm.Duration > BlinkDetector.BlinkMaxDuration).Count()}",
        ]);
    }

    public void Dispose()
    {
        PeakDetector.Save(DataSourceType.Finger, HandPeakDetector);
        PeakDetector.Save(DataSourceType.Eye, GazePeakDetector);
        BlinkDetector.Save(BlinkDetector);

        GC.SuppressFinalize(this);
    }

    // Internal

    readonly System.Drawing.Color COLOR_FINGER = System.Drawing.Color.Blue;
    readonly System.Drawing.Color COLOR_GAZE = System.Drawing.Color.Red;
    readonly System.Drawing.Color COLOR_BLINK = System.Drawing.Color.Gray;

    List<Vdl> _vdls = [];

    private (Sample[], Sample[]) GetTimeseries(Vdl vdl) => (
            Settings.Finger switch
            {
                Finger.Index => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.HandIndex.Y)).ToArray(),
                Finger.Middle => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.HandMiddle.Y)).ToArray(),
                _ => throw new NotImplementedException($"{Settings.Finger} hand data source is not yet supported")
            },
            Settings.GazeRotation switch
            {
                GazeRotation.Yaw => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.Eye.Yaw)).ToArray(),
                GazeRotation.Pitch => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.Eye.Pitch)).ToArray(),
                _ => throw new NotImplementedException($"{Settings.GazeRotation} gaze data source is not yet supported")
            }
        );

    private (Peak, Peak)[] MatchPeaks(Peak[] finger, Peak[] gaze)
    {
        var result = new List<(Peak, Peak)>();

        int gazeIndex = 0;
        foreach (Peak fingerPeak in finger)
        {
            while (gazeIndex < gaze.Length)
            {
                var gazePeak = gaze[gazeIndex++];
                if (Math.Abs(gazePeak.TimestampStart - fingerPeak.TimestampStart) < Settings.MaxFingerGazeDelay)
                {
                    result.Add((fingerPeak, gazePeak));
                    break;
                }
                else if (gazePeak.TimestampStart > fingerPeak.TimestampStart)
                {
                    gazeIndex -= 1;
                    break;
                }
            }
        }

        return result.ToArray();
    }
}
