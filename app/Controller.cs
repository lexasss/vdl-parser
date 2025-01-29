using System.Collections.ObjectModel;

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

public class Controller : IDisposable
{
    public ObservableCollection<Vdl> Vdls { get; }
    public PeakDetector HandPeakDetector { get; } = PeakDetector.Load(DataSourceType.Finger);
    public PeakDetector GazePeakDetector { get; } = PeakDetector.Load(DataSourceType.Eye);
    public Finger Finger { get; set; } = Finger.Index;
    public GazeRotation GazeRotation { get; set; } = GazeRotation.Yaw;

    public Controller()
    {
        Vdls = new ObservableCollection<Vdl>(_vdls);
    }

    public void Add(Vdl vdl)
    {
        Vdls.Add(vdl);
    }

    public void Display(Vdl vdl, Graph plot)
    {
        plot.Reset();

        var fingerTimeseries = Finger switch
        {
            Finger.Index => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.HandIndex.Y)).ToArray(),
            Finger.Middle => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.HandMiddle.Y)).ToArray(),
            _ => throw new NotImplementedException($"{Finger} is not yet supported")
        };

        var gazeTimeseries = GazeRotation switch
        {
            GazeRotation.Yaw => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.Eye.Yaw)).ToArray(),
            GazeRotation.Pitch => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.Eye.Pitch)).ToArray(),
            _ => throw new NotImplementedException($"{GazeRotation} is not yet supported")
        };

        plot.AddCurve(fingerTimeseries, COLOR_FINGER);
        plot.AddCurve(gazeTimeseries, COLOR_GAZE);
    }

    public void DetectPeaks(Vdl vdl, Graph plot)
    {
        Display(vdl, plot);

        var fingerTimeseries = Finger switch
        {
            Finger.Index => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.HandIndex.Y)).ToArray(),
            Finger.Middle => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.HandMiddle.Y)).ToArray(),
            _ => throw new NotImplementedException($"{Finger} is not yet supported")
        };

        var fingerPeaks = HandPeakDetector.Find(fingerTimeseries);
        foreach (var peak in fingerPeaks)
        {
            plot.AddVLine(peak.TimestampStart, COLOR_FINGER);
        }

        var gazeTimeseries = GazeRotation switch
        {
            GazeRotation.Yaw => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.Eye.Yaw)).ToArray(),
            GazeRotation.Pitch => vdl.Records.Select(record => new Sample(record.TimestampSystem, record.Eye.Pitch)).ToArray(),
            _ => throw new NotImplementedException($"{GazeRotation} is not yet supported")
        };

        var gazePeaks = GazePeakDetector.Find(gazeTimeseries);
        foreach (var peak in gazePeaks)
        {
            plot.AddVLine(peak.TimestampStart, COLOR_GAZE);
        }
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
}
