using System.ComponentModel;

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

public record class TimestampedNbtEvent(long Timestamp, NBackTaskEvent Event);

public class Processor
{
    public PeakDetector HandPeakDetector { get; } = PeakDetector.Load(DataSourceType.Hand);
    public PeakDetector GazePeakDetector { get; } = PeakDetector.Load(DataSourceType.Gaze);
    public BlinkDetector BlinkDetector { get; } = BlinkDetector.Load();

    public Sample[] HandSamples { get; private set; } = [];
    public Sample[] GazeSamples { get; private set; } = [];
    public Peak[] HandPeaks { get; private set; } = [];
    public Peak[] GazePeaks { get; private set; } = [];
    public Trial[] Trials { get; private set; } = [];
    public GazeDataMiss[] GazeDataMisses { get; private set; } = [];
    public double[] PupilSizes { get; private set; } = [];
    public TimestampedNbtEvent[] NBackTaskEvents { get; private set; } = [];

    public Vdl? Vdl { get; private set; } = null;

    public void SaveDetectors()
    {
        PeakDetector.Save(DataSourceType.Hand, HandPeakDetector);
        PeakDetector.Save(DataSourceType.Gaze, GazePeakDetector);
        BlinkDetector.Save(BlinkDetector);
    }

    public void Feed(Vdl vdl)
    {
        Vdl = vdl;

        var records = Vdl.Records;

        (HandSamples, GazeSamples) = GetHandGazeSamples(records);

        HandPeaks = HandPeakDetector.Find(HandSamples);
        GazePeaks = GazePeakDetector.Find(GazeSamples);

        Trials = Trial.GetTrials(records, HandPeaks, GazePeaks);

        GazeDataMisses = BlinkDetector.Find(GazeSamples);

        PupilSizes = GetPupilSizes(records);

        NBackTaskEvents = GetNBackTaskEvents(records);
    }

    public static long GetTimestamp(VdlRecord record) => _settings.TimestampSource switch
    {
        TimestampSource.Headset => record.TimestampHeadset,
        TimestampSource.System => record.TimestampSystem,
        _ => throw new NotSupportedException($"{_settings.TimestampSource} timestamp source is not supported"),
    };

    // Internal

    static readonly Settings _settings = Settings.Instance;

    public static (Sample[], Sample[]) GetHandGazeSamples(VdlRecord[] records)
    {
        return (
            _settings.HandDataSource switch
            {
                HandDataSource.IndexFinger => records
                    .Select(record => new Sample(GetTimestamp(record), record.HandIndex.Y))
                    .ToArray(),
                HandDataSource.MiddleFinger => records
                    .Select(record => new Sample(GetTimestamp(record), record.HandMiddle.Y))
                    .ToArray(),
                _ => throw new NotImplementedException($"{_settings.HandDataSource} hand data source is not yet supported")
            },
            _settings.GazeDataSource switch
            {
                GazeDataSource.YawRotation => records
                    .Select(record => new Sample(GetTimestamp(record), record.Eye.Yaw))
                    .ToArray(),
                GazeDataSource.PitchRotation => records
                    .Select(record => new Sample(GetTimestamp(record), record.Eye.Pitch))
                    .ToArray(),
                _ => throw new NotImplementedException($"{_settings.GazeDataSource} gaze data source is not yet supported")
            }
        );
    }

    private static double[] GetPupilSizes(VdlRecord[] records) => records
        .SkipWhile(record => record.NBackTaskEvent?.Type != NBackTaskEventType.SessionStart)
        .TakeWhile(record => record.NBackTaskEvent?.Type != NBackTaskEventType.SessionEnd)
        .Where(record => record.LeftPupil.Openness > 0.6 && record.RightPupil.Openness > 0.6)
        .Select(record => (record.LeftPupil.Size + record.RightPupil.Size) / 2)
        .ToArray();

    private static TimestampedNbtEvent[] GetNBackTaskEvents(VdlRecord[] records) => records
        .Where(r => r.NBackTaskEvent != null)
        .Select(record => new TimestampedNbtEvent(GetTimestamp(record), record.NBackTaskEvent!))
        .ToArray();
}
