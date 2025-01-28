using System.Collections.ObjectModel;

namespace VdlParser;

public class Controller
{
    public ObservableCollection<Vdl> Vdls { get; }
    public PeakDetector HandPeakDetector { get; } = new PeakDetector() { PeakThreshold = 1.5, BufferSize = 12, IgnoranceThrehold = 20 };
    public PeakDetector GazePeakDetector { get; } = new PeakDetector() { PeakThreshold = 10, BufferSize = 30, IgnoranceThrehold = -1000 };

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

        var middleFingerTimeseries = vdl.Records.Select(record => new Sample(record.TimestampSystem, record.HandMiddle.Y)).ToArray();
        var middleFingerPeaks = HandPeakDetector.Find(middleFingerTimeseries);

        var indexFingerTimeseries = vdl.Records.Select(record => new Sample(record.TimestampSystem, record.HandIndex.Y)).ToArray();
        var indexFingerPeaks = HandPeakDetector.Find(indexFingerTimeseries);

        System.Diagnostics.Debug.WriteLine("Index finger");
        foreach (var peak in indexFingerPeaks)
        {
            System.Diagnostics.Debug.WriteLine($"{peak}");
        }

        System.Diagnostics.Debug.WriteLine("Middle finger");
        foreach (var peak in middleFingerPeaks)
        {
            System.Diagnostics.Debug.WriteLine($"{peak}");
        }

        var gazeYawTimeseries = vdl.Records.Select(record => new Sample(record.TimestampSystem, record.Eye.Yaw)).ToArray();
        var gazeYawPeaks = GazePeakDetector.Find(gazeYawTimeseries);

        System.Diagnostics.Debug.WriteLine("Gaze yaw");
        foreach (var peak in gazeYawPeaks)
        {
            System.Diagnostics.Debug.WriteLine($"{peak}");
        }

        plot.AddCurve(middleFingerTimeseries, System.Drawing.Color.Green);
        plot.AddCurve(indexFingerTimeseries, System.Drawing.Color.Blue);
        plot.AddCurve(gazeYawTimeseries, System.Drawing.Color.Black);
    }

    // Internal

    List<Vdl> _vdls = new();
}
