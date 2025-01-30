using System.Collections.ObjectModel;
using System.Reflection;

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
        var (fingerTs, gazeTs) = GetTimeseries(vdl);

        plot.Reset();
        plot.AddCurve(fingerTs, COLOR_FINGER);
        plot.AddCurve(gazeTs, COLOR_GAZE);

        State = ControllerState.RawDataDisplayed;
    }

    public string AnalyzeAndDraw(Vdl vdl, Graph plot)
    {
        var (fingerTs, gazeTs) = GetTimeseries(vdl);

        var fingerPeaks = HandPeakDetector.Find(fingerTs);
        var gazePeaks = GazePeakDetector.Find(gazeTs);

        // Draw
        plot.Reset();
        plot.AddCurve(fingerTs, COLOR_FINGER);
        plot.AddCurve(gazeTs, COLOR_GAZE);

        foreach (var peak in fingerPeaks)
        {
            plot.AddVLine(peak.TimestampStart, COLOR_FINGER);
        }

        foreach (var peak in gazePeaks)
        {
            plot.AddVLine(peak.TimestampStart, COLOR_GAZE);
        }

        var matches = MatchPeaks(fingerPeaks, gazePeaks);

        foreach (var match in matches)
        {
            plot.AddVLine(match.Item1.TimestampStart, COLOR_FINGER, 2);
            plot.AddVLine(match.Item2.TimestampStart, COLOR_GAZE, 2);
        }

        State = ControllerState.PeaksDetected;

        return string.Join('\n', [
            $"Sample count: {vdl.RecordCount}",
            $"Finger peak count: {fingerPeaks.Length}",
            $"Gaze peak count: {gazePeaks.Length}",
            $"Matches:",
            $"  count = {matches.Length}",
            $"  avg delay = {ComputeAverageDelay(matches)} ms",
        ]);
    }

    public void Dispose()
    {
        PeakDetector.Save(DataSourceType.Finger, HandPeakDetector);
        PeakDetector.Save(DataSourceType.Eye, GazePeakDetector);
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly System.Drawing.Color COLOR_FINGER = System.Drawing.Color.Blue;
    readonly System.Drawing.Color COLOR_GAZE = System.Drawing.Color.Red;

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

    private long ComputeAverageDelay((Peak, Peak)[] pairs)
    {
        long sum = 0;
        foreach (var pair in pairs)
        {
            sum += pair.Item2.TimestampStart - pair.Item1.TimestampStart;
        }
        return pairs.Length > 0 ? sum / pairs.Length : 0;
    }
}
