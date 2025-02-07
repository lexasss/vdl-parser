﻿namespace VdlParser;

public class Trial(Peak? handPeak, Peak? gazePeak, long startTimestamp, long responseTimestamp, bool isCorrect)
{
    public Peak? HandPeak => handPeak;
    public Peak? GazePeak => gazePeak;

    public bool HasHandGazeMatch => handPeak != null && gazePeak != null;
    public long StartTimestamp => startTimestamp;
    public long ResponseTimestamp => responseTimestamp;
    public long GazeHandInterval => HasHandGazeMatch ? GazePeak!.TimestampStart - HandPeak!.TimestampStart : 0;
    public bool IsCorrect => isCorrect;

    public static Trial[] GetTrials(Record[] records, Peak[] handPeaks, Peak[] gazePeaks)
    {
        var result = new List<Trial>();

        Settings settings = Settings.Instance;


        long timestampStart = 0;
        long timestampResponse = 0;
        bool isCorrect = false;

        foreach (var record in records)
        {
            if (record.NBackTaskEvent is NBackTaskTrial trial)
            {
                if (trial.Type == NBackTaskEventType.TrialStart)
                    timestampStart = GetTimestamp(record, settings.TimestampSource);
                else if (trial.Type == NBackTaskEventType.TrialResponse)
                    timestampResponse = GetTimestamp(record, settings.TimestampSource);
                else if (trial.Type == NBackTaskEventType.TrialEnd)
                {
                    var timestampEnd = GetTimestamp(record, settings.TimestampSource);
                    isCorrect = (trial as NBackTaskTrialResult)?.IsCorrect == true;

                    var handPeak = handPeaks.FirstOrDefault(peak => 
                        peak.TimestampStart > timestampStart && 
                        peak.TimestampStart < timestampEnd);
                    var gazePeak = gazePeaks.FirstOrDefault(peak => 
                        peak.TimestampStart > timestampStart &&
                        peak.TimestampStart < timestampEnd &&
                        Math.Abs(peak.TimestampStart - (handPeak?.TimestampStart ?? 0)) < settings.MaxHandGazeDelay);

                    result.Add(new Trial(handPeak, gazePeak, timestampStart, timestampResponse, isCorrect));
                }
            }
        }
        /*
        int recordIndex = 0;
        int gazeIndex = 0;
        foreach (Peak handPeak in handPeaks)
        {
            long timestampStart = 0;
            long timestampResponse = 0;
            bool isCorrect = false;

            while (recordIndex < records.Length)
            {
                var record = records[recordIndex++];
                if (record.NBackTaskEvent?.Type == NBackTaskEventType.TrialStart)
                {
                    timestampStart = GetTimestamp(record, settings.TimestampSource);
                }
                else if (record.NBackTaskEvent?.Type == NBackTaskEventType.TrialResponse)
                {
                    timestampResponse = GetTimestamp(record, settings.TimestampSource);
                }
                else if (record.NBackTaskEvent?.Type == NBackTaskEventType.TrialEnd)
                {
                    isCorrect = ((NBackTaskTrialResult)record.NBackTaskEvent).IsCorrect;
                    break;
                }
            }

            while (gazeIndex < gazePeaks.Length)
            {
                var gazePeak = gazePeaks[gazeIndex++];
                if (Math.Abs(gazePeak.TimestampStart - handPeak.TimestampStart) < settings.MaxHandGazeDelay)
                {
                    result.Add(new Trial(handPeak, gazePeak, timestampStart, timestampResponse, isCorrect));
                    break;
                }
                else if (gazePeak.TimestampStart > handPeak.TimestampStart)
                {
                    gazeIndex -= 1;
                    break;
                }
            }
        }*/

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
