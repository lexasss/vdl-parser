namespace VdlParser;

public class Trial(Peak handPeak, Peak gazePeak, long timestampStart, long timestampResponse)
{
    public Peak HandPeak => handPeak;
    public Peak GazePeak => gazePeak;

    public long TimestampStart => timestampStart;
    public long TimestampResponse => timestampResponse;
    public long GazeHandInterval => GazePeak.TimestampStart - HandPeak.TimestampStart;

    public static Trial[] GetTrials(Record[] records, Peak[] handPeaks, Peak[] gazePeaks, long maxHandGazeDelay, TimestampSource timestampSource)
    {
        var result = new List<Trial>();

        int recordIndex = 0;
        int gazeIndex = 0;
        foreach (Peak handPeak in handPeaks)
        {
            long timestampStart = 0;
            long timestampResponse = 0;

            while (recordIndex < records.Length)
            {
                var record = records[recordIndex++];
                if (record.NBackTaskEvent?.Type == NBackTaskEventType.TrialStart)
                {
                    timestampStart = GetTimestamp(record, timestampSource);
                }
                else if (record.NBackTaskEvent?.Type == NBackTaskEventType.TrialResponse)
                {
                    timestampResponse = GetTimestamp(record, timestampSource);
                    break;
                }
            }

            while (gazeIndex < gazePeaks.Length)
            {
                var gazePeak = gazePeaks[gazeIndex++];
                if (Math.Abs(gazePeak.TimestampStart - handPeak.TimestampStart) < maxHandGazeDelay)
                {
                    result.Add(new Trial(handPeak, gazePeak, timestampStart, timestampResponse));
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

    // Interval

    private static long GetTimestamp(Record record, TimestampSource source) => source switch
    {
        TimestampSource.Headset => record.TimestampHeadset,
        TimestampSource.System => record.TimestampSystem,
        _ => throw new NotSupportedException($"{source} timestamp source is not supported"),
    };
}
