using System.Collections.ObjectModel;

namespace VdlParser;

public class Controller : IDisposable
{
    public ObservableCollection<Vdl> Vdls { get; }
    public PeakDetector HandPeakDetector { get; } = PeakDetector.Load(DataSourceType.Hand);
    public PeakDetector GazePeakDetector { get; } = PeakDetector.Load(DataSourceType.Gaze);
    public BlinkDetector BlinkDetector { get; } = BlinkDetector.Load();

    public Controller()
    {
        Vdls = new ObservableCollection<Vdl>(_vdls);
    }

    public void Add(Vdl vdl)
    {
        Vdls.Add(vdl);
    }

    public void Dispose()
    {
        PeakDetector.Save(DataSourceType.Hand, HandPeakDetector);
        PeakDetector.Save(DataSourceType.Gaze, GazePeakDetector);
        BlinkDetector.Save(BlinkDetector);

        GC.SuppressFinalize(this);
    }

    // Internal

    List<Vdl> _vdls = [];
}
