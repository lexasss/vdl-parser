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
    public Record[] Records { get; }

    public Sample[] HandSamples { get; }
    public Sample[] GazeSamples { get; }
    public Peak[] HandPeaks { get; }
    public Peak[] GazePeaks { get; }
    public Trial[] Trials { get; }
    public GazeDataMiss[] GazeDataMisses { get; }
    public double[] PupilSizes { get; }
    public TimestampedNbtEvent[] NBackTaskEvents { get; }

    public Processor(Record[] records, Controller controller)
    {
        Records = records;

        (HandSamples, GazeSamples) = GetHandGazeSamples(records);

        HandPeaks = controller.HandPeakDetector.Find(HandSamples);
        GazePeaks = controller.GazePeakDetector.Find(GazeSamples);

        Trials = Trial.GetTrials(records, HandPeaks, GazePeaks);

        GazeDataMisses = controller.BlinkDetector.Find(GazeSamples);

        PupilSizes = GetPupilSizes(records);

        NBackTaskEvents = GetNBackTaskEvents(records);
    }

    public static long GetTimestamp(Record record) => _settings.TimestampSource switch
    {
        TimestampSource.Headset => record.TimestampHeadset,
        TimestampSource.System => record.TimestampSystem,
        _ => throw new NotSupportedException($"{_settings.TimestampSource} timestamp source is not supported"),
    };

    // Internal

    static readonly Settings _settings = Settings.Instance;

    public static (Sample[], Sample[]) GetHandGazeSamples(Record[] records)
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

    private static double[] GetPupilSizes(Record[] records) => records
        .SkipWhile(record => record.NBackTaskEvent?.Type != NBackTaskEventType.SessionStart)
        .TakeWhile(record => record.NBackTaskEvent?.Type != NBackTaskEventType.SessionEnd)
        .Where(record => record.LeftPupil.Openness > 0.6 && record.RightPupil.Openness > 0.6)
        .Select(record => (record.LeftPupil.Size + record.RightPupil.Size) / 2)
        .ToArray();

    private static TimestampedNbtEvent[] GetNBackTaskEvents(Record[] records) => records
        .Where(r => r.NBackTaskEvent != null)
        .Select(record => new TimestampedNbtEvent(GetTimestamp(record), record.NBackTaskEvent!))
        .ToArray();
}
