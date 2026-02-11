using MathNet.Numerics.Statistics;
using System.Windows;
using VdlParser.Detectors;

namespace VdlParser.Models;

public class Trial(Peak? handPeak, Peak? firstGazePeak, Peak? lastGazePeak,
    long startTimestamp, long responseTimestamp, long totalGazeDuration,
    bool isCorrect, bool hasValidData)
{
    public Peak? HandPeak => handPeak;
    public Peak? LastGazePeak => lastGazePeak;
    public Peak? FirstGazePeak => firstGazePeak;

    public bool HasHandGazeMatch => handPeak != null && lastGazePeak != null;
    public long StartTimestamp => startTimestamp;
    public long ResponseTimestamp => responseTimestamp;
    public long GazeHandInterval => HasHandGazeMatch ? LastGazePeak!.TimestampStart - HandPeak!.TimestampStart : 0;
    public long TotalGazeDuration => totalGazeDuration;
    public bool IsCorrect => isCorrect;
    public bool HasValidData => hasValidData;

    public static Trial[] GetTrials(VdlRecord[] records, Peak[] handPeaks, Peak[] gazePeaks, string? testInfo = null)
    {
        var result = new List<Trial>();

        GeneralSettings settings = GeneralSettings.Instance;

        long timestampStart = 0;
        long timestampResponse = 0;
        long timestampEnd = 0;
        long invalidTrialCount = 0;
        long fixedTrialCount = 0;
        long latestStartTimestamp = 0;
        NBackTaskEventType? lastEventType = null;       // NBT logging bugfix

        foreach (var record in records)
        {
            if (record.NBackTaskEvent is NBackTaskTrial nbtTrial)
            {
                if (nbtTrial.Type == NBackTaskEventType.TrialStart)
                {
                    if (timestampResponse > 0 && timestampEnd == 0)     // NBT logging bugfix. On second and the rest trial start events: check against missing trial end event (RES)
                    {
                        if (record.TimestampSystem - latestStartTimestamp > MIN_TRIAL_LENGTH)
                        {
                            var trial = GetTrialWithPeaks(records, handPeaks, gazePeaks, latestStartTimestamp, timestampResponse, record.TimestampSystem, true, settings);
                            result.Add(trial);
                        }
                        fixedTrialCount++;
                    }
                    timestampStart = GetTimestamp(record, settings.TimestampSource);
                    latestStartTimestamp = timestampStart;
                    timestampEnd = 0;
                }
                else if (nbtTrial.Type == NBackTaskEventType.TrialResponse && lastEventType != NBackTaskEventType.TrialResponse)
                {
                    timestampResponse = GetTimestamp(record, settings.TimestampSource);
                }
                else if (nbtTrial.Type == NBackTaskEventType.TrialEnd)
                {
                    if (timestampStart == 0)
                    {
                        invalidTrialCount++;
                    }
                    else
                    {
                        timestampEnd = GetTimestamp(record, settings.TimestampSource);
                        var isCorrect = (nbtTrial as NBackTaskTrialResult)?.IsCorrect == true;

                        if (record.TimestampSystem - latestStartTimestamp > MIN_TRIAL_LENGTH)
                        {
                            var trial = GetTrialWithPeaks(records, handPeaks, gazePeaks, timestampStart, timestampResponse, timestampEnd, isCorrect, settings);
                            result.Add(trial);
                        }

                        timestampStart = 0;
                    }
                }

                lastEventType = nbtTrial.Type;
            }
        }

        if (invalidTrialCount > 0)
        {
            MessageBox.Show($"Found {invalidTrialCount} trials without start! Please fix your data ({testInfo ?? string.Empty}).", "Vdl parser");
        }
        if (fixedTrialCount > 0)
        {
            System.Diagnostics.Debug.WriteLine($"Fixed {fixedTrialCount} trials without trial-end event");
        }

        return result.ToArray();
    }

    // Interval

    const int MIN_TRIAL_LENGTH = 0; // ms - ignored in the current implementation

    private static Trial GetTrialWithPeaks(VdlRecord[] records,
        Peak[] handPeaks, Peak[] gazePeaks,
        long trialStartTimestamp, long trialResponseTimestamp, long trialEndTimestamp,
        bool isCorrect, GeneralSettings settings)
    {
        var gazeMovementStd = GetStandardDeviation(records, settings.GazeDataSource);

        var trialRecords = records
            .SkipWhile(r => r.TimestampSystem < trialStartTimestamp)
            .TakeWhile(r => r.TimestampSystem < trialEndTimestamp);

        bool isGazeMoving = GetStandardDeviation(trialRecords, settings.GazeDataSource) > (gazeMovementStd * settings.GazeMovementFactor);
        double handInvalidDataShare = GetInvaldidDataShare(trialRecords, settings.HandDataSource);

        var handPeak = handPeaks.LastOrDefault(peak =>
            peak.TimestampStart > trialStartTimestamp &&
            peak.TimestampStart < trialResponseTimestamp);
        var trialGazePeaks = gazePeaks.Where(peak =>
            peak.TimestampStart > (trialStartTimestamp - settings.MaxTrialStartToGazePeakStartInterval) &&
            peak.TimestampStart < trialEndTimestamp &&
            peak.TimestampEnd < trialEndTimestamp + settings.MaxGazePeakEndToNextTrialStartInterval &&
            (handPeak == null || peak.TimestampStart < (handPeak.TimestampStart + settings.MaxGazePeakStartToHandPeakStartInterval)) && 
            peak.TimestampStart < (trialResponseTimestamp - settings.MinResponseToGazePeakStartInterval)
            );

        var lastGazePeak = trialGazePeaks.LastOrDefault();
        var firstGazePeak = trialGazePeaks.FirstOrDefault();
        var totalGazeDuration = trialGazePeaks.Sum(peak => peak.Duration);

        bool isValid = handPeak != null && handInvalidDataShare < settings.MaxInvalidHandDataShare; // && isHandMoving;

        if (lastGazePeak != null && handPeak != null &&
            Math.Abs(lastGazePeak.TimestampStart - (handPeak?.TimestampStart ?? 0)) > settings.MaxHandGazeDelay)
        {
            isValid = false;
        }
        else if (handPeak != null && lastGazePeak == null && isGazeMoving)
        {
            isValid = false;
        }

        return new Trial(handPeak, firstGazePeak, lastGazePeak, trialStartTimestamp, trialResponseTimestamp, totalGazeDuration, isCorrect, isValid);
    }


    private static long GetTimestamp(VdlRecord record, TimestampSource source) => source switch
    {
        TimestampSource.Headset => record.TimestampHeadset,
        TimestampSource.System => record.TimestampSystem,
        _ => throw new NotSupportedException($"{source} timestamp source is not supported"),
    };

    private static double GetInvaldidDataShare(IEnumerable<VdlRecord> records, HandDataSource handDataSource)
    {
        var notrackingRecords = records.Where(r => 0 == handDataSource switch
        {
            HandDataSource.Palm or HandDataSource.IndexFinger or HandDataSource.MiddleFinger => r.HandPalm.X,
            HandDataSource.TopViewPalm or HandDataSource.TopViewIndexFinger or HandDataSource.TopViewMiddleFinger => r.TopViewHandPalm.X,
            _ => throw new NotSupportedException()
        });

        return (double)notrackingRecords.Count() / records.Count();
    }

    private static double GetStandardDeviation(IEnumerable<VdlRecord> records, GazeDataSource gazeDataSource)
    {
        var trialGazeData = records.Select(r => gazeDataSource switch
        {
            GazeDataSource.YawRotation => r.Eye.Yaw,
            GazeDataSource.PitchRotation => r.Eye.Pitch,
            _ => throw new NotImplementedException($"{gazeDataSource} gaze data source is not yet supported")
        });

        return trialGazeData.StandardDeviation();
    }

    private static double GetStandardDeviation(IEnumerable<VdlRecord> records, HandDataSource handDataSource)
    {
        var trialHandData = records.Select(r => handDataSource switch
        {
            HandDataSource.Palm or HandDataSource.IndexFinger or HandDataSource.MiddleFinger => r.HandPalm.Z,
            HandDataSource.TopViewPalm or HandDataSource.TopViewIndexFinger or HandDataSource.TopViewMiddleFinger => r.TopViewHandPalm.Z,
            _ => throw new NotSupportedException()
        });

        return trialHandData.StandardDeviation();
    }
}
