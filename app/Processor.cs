using System.ComponentModel;
using VdlParser.Detectors;
using VdlParser.Models;

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
    public HandPeakDetector HandPeakDetector { get; } = Storage.Load<HandPeakDetector>();
    public GazePeakDetector GazePeakDetector { get; } = Storage.Load<GazePeakDetector>();
    public BlinkDetector BlinkDetector { get; } = Storage.Load<BlinkDetector>();
    public BlinkDetector2 BlinkDetector2 { get; } = Storage.Load<BlinkDetector2>();

    public Sample[] HandSamples { get; private set; } = [];
    public Sample[] GazeSamples { get; private set; } = [];
    public Sample[] PupilSizeSamples { get; private set; } = [];
    public Sample[] PupilOpennessSamples { get; private set; } = [];

    public Peak[] HandPeaks { get; private set; } = [];
    public Peak[] GazePeaks { get; private set; } = [];
    public Trial[] Trials { get; private set; } = [];
    public GazeDataMiss[] GazeDataMisses { get; private set; } = [];
    public Blink[] Blinks { get; private set; } = [];
    public double[] PupilSizes { get; private set; } = [];
    public TimestampedNbtEvent[] NBackTaskEvents { get; private set; } = [];

    public Vdl? Vdl { get; private set; } = null;

    public void SaveSettings()
    {
        Storage.Save(HandPeakDetector);
        Storage.Save(GazePeakDetector);
        Storage.Save(BlinkDetector);
        Storage.Save(BlinkDetector2);
    }

    public void SetVdl(Vdl vdl)
    {
        Vdl = vdl;

        var records = Vdl.Records;

        HandSamples = GetHandSamples(records);
        GazeSamples = GetGazeSamples(records);

        PupilSizeSamples = records
            .Select(record => new Sample(GetTimestamp(record), record.PupilSize))
            .ToArray();
        PupilOpennessSamples = records
            .Select(record => new Sample(GetTimestamp(record), record.PupilOpenness))
            .ToArray();
    }

    public void Process()
    {
        if (Vdl == null)
            return;

        var records = Vdl.Records;

        HandPeaks = HandPeakDetector.Find(HandSamples);
        GazePeaks = GazePeakDetector.Find(GazeSamples);

        Trials = Trial.GetTrials(records, HandPeaks, GazePeaks);

        GazeDataMisses = BlinkDetector.Find(GazeSamples);
        Blinks = BlinkDetector2.Find(records);

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

    static readonly GeneralSettings _settings = GeneralSettings.Instance;

    private static Sample[] GetHandSamples(VdlRecord[] records) => _settings.HandDataSource switch
    {
        HandDataSource.IndexFinger => records
            .Select(record => new Sample(GetTimestamp(record), record.HandIndex.Y))
            .ToArray(),
        HandDataSource.MiddleFinger => records
            .Select(record => new Sample(GetTimestamp(record), record.HandMiddle.Y))
            .ToArray(),
        _ => throw new NotImplementedException($"{_settings.HandDataSource} hand data source is not yet supported")
    };

    private static Sample[] GetGazeSamples(VdlRecord[] records) => _settings.GazeDataSource switch
    {
        GazeDataSource.YawRotation => records
            .Select(record => new Sample(GetTimestamp(record), record.Eye.Yaw))
            .ToArray(),
        GazeDataSource.PitchRotation => records
            .Select(record => new Sample(GetTimestamp(record), record.Eye.Pitch))
            .ToArray(),
        _ => throw new NotImplementedException($"{_settings.GazeDataSource} gaze data source is not yet supported")
    };

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
