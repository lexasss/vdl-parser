using MathNet.Numerics.Statistics;

namespace VdlParser.Models;

public class VdlStatistics(Processor processor) : IStatistics
{
    public string Get(Format format)
    {
        var sessionStartTimestamp = processor.NBackTaskEvents.FirstOrDefault(evt => evt.Event.Type == NBackTaskEventType.SessionStart)?.Timestamp ?? 0;
        var sessionEndTimestamp = processor.NBackTaskEvents.FirstOrDefault(evt => evt.Event.Type == NBackTaskEventType.SessionEnd)?.Timestamp ?? processor.Trials[^1].ResponseTimestamp;

        var trialsWithValidData = processor.Trials.Where(trial => trial.HasValidData);

        var gazeHandMatches = trialsWithValidData.Where(trial => trial.HasHandGazeMatch);
        var gazeHandMatchCount = gazeHandMatches.Count();
        var matchesCountPercentage = 100.0 * gazeHandMatchCount / trialsWithValidData.Count();

        var responseIntervals = processor.Trials
            .Where(trial => trial.ResponseTimestamp > 0)
            .Select(trial => (double)(trial.ResponseTimestamp - trial.StartTimestamp));
        var (responseIntervalMean, responseIntervalStd) = responseIntervals.MeanStandardDeviation();
        var gazeHandIntervals = gazeHandMatches.Select(trial => -(double)trial.GazeHandInterval);
        var (gazeHandIntervalMean, gazeHandIntervalStd) = gazeHandIntervals.MeanStandardDeviation();
        var gazeHandIntervalMedian = gazeHandIntervals.Median();
        var glanceDurations = processor.GazePeaks.Select(peak => (double)(peak.TimestampEnd - peak.TimestampStart));
        var (glanceDurationMean, glanceDurationStd) = glanceDurations.MeanStandardDeviation();
        var (pupilSizeMean, pupilSizeStd) = processor.PupilSizes.MeanStandardDeviation();
        var blinkCount2 = processor.Blinks.Count();
        var longEyeLostCount = processor.GazeDataMisses
            .Where(gdm => gdm.IsLong)
            .Count();
        var tb = new TemproralBids();
        var gazeHandIntervalBids = tb.Get(gazeHandMatches
                .Select(trial => new Timestamped(trial.StartTimestamp, -trial.GazeHandInterval))
                .ToArray(),
                sessionStartTimestamp,
                sessionEndTimestamp)
            .Select(bid => Math.Round(bid.Mean));
        var matchBids = tb.Get(processor.Trials
                .Select(trial => new Timestamped(trial.StartTimestamp, trial.HasHandGazeMatch ? 1 : 0))
                .ToArray(),
                sessionStartTimestamp,
                sessionEndTimestamp)
            .Select(bid => bid.Mean).ToArray();
        var correctResponses = (double)processor.Trials.Sum(trial => trial.IsCorrect ? 1 : 0) / processor.Trials.Length;
        var calibratedPupilSizes = processor.PupilSizes.Select(size => size - (processor.Vdl?.PupilCalibration?.Size ?? 0));
        var blinkCount = processor.GazeDataMisses
            .Where(gdm => gdm.IsBlink)
            .Count();

        var handMovementOnsetDelays = gazeHandMatches
            .Select(trial => trial.HandPeak == null ? 0d : trial.HandPeak.TimestampStart - trial.StartTimestamp)
            .Where(delay => delay > 0);
        var (handMovementOnsetDelayMean, handMovementOnsetDelayStd) = handMovementOnsetDelays.MeanStandardDeviation();
        var handSelectionDurations = trialsWithValidData
            .Select(trial => trial.HandPeak == null ? 0d : trial.ResponseTimestamp - trial.HandPeak.TimestampStart)
            .Where(delay => delay > 0);
        var (handSelectionDurationMean, handSelectionDurationStd) = handSelectionDurations.MeanStandardDeviation();
        var firstGazeMovementOnsetDelays = trialsWithValidData
            .Select(trial => trial.FirstGazePeak == null ? -1d : Math.Max(0, trial.FirstGazePeak.TimestampStart - trial.StartTimestamp))
            .Where(delay => delay >= 0);
        var (firstGazeMovementOnsetDelayMean, firstGazeMovementOnsetDelayStd) = firstGazeMovementOnsetDelays.MeanStandardDeviation();
        var selectionFixationDurations = trialsWithValidData
            .Select(trial => (double)(trial.LastGazePeak?.Duration ?? 0))
            .Where(duration => duration > 0);
        var (selectionFixationDurationMean, selectionFixationDurationStd) = selectionFixationDurations.MeanStandardDeviation();
        var totalGazeDurations = trialsWithValidData
            .Select(trial => (double)trial.TotalGazeDuration)
            .Where(duration => duration > 0);
        var (totalGazeDurationMean, totalGazeDurationStd) = totalGazeDurations.MeanStandardDeviation();
        var gazeFollowsHandCount = gazeHandMatches.Count(trial => trial.GazeHandInterval < 0);

        var handGazeIntersections = processor.HandGazeCommonPeakIntervals.Select(intersection => (double)intersection.Duration);
        var (handGazeIntersectionMean, handGazeIntersectionStd) = handGazeIntersections.MeanStandardDeviation();
        var trialsWithHandPeak = processor.Trials.Where(trial => trial.HandPeak != null);
        var handGazeIntersectionPerTrial = handGazeIntersections.Sum() / trialsWithHandPeak.Count();

        var ql = GeneralSettings.Instance.QuantileThreshold;
        var qh = 1.0 - ql;

        string[] emptyBids = [NODATA, NODATA, NODATA, NODATA, NODATA];

        if (format == Format.List)
            return string.Join('\n', [
                $"Hand/Gaze peaks: {processor.HandPeaks.Length}/{processor.GazePeaks.Length}",
                $"  match count = {gazeHandMatchCount} ({matchesCountPercentage:F1}%)",
                $"Correct responses = {correctResponses*100:F1}%",
                $"Response delay",
                $"  mean = {responseIntervalMean:F0} ms (SD = {responseIntervalStd:F1} ms)",
                $"  median = {responseIntervals.Median():F0} ms ({responseIntervals.Quantile(ql):F0}..{responseIntervals.Quantile(qh):F0} ms)",
                $"Gaze advance",
                $"  mean = {gazeHandIntervalMean:F0} ms (SD = {gazeHandIntervalStd:F1} ms)",
                $"  median = {gazeHandIntervalMedian:F0} ms ({gazeHandIntervals.Quantile(ql):F0}..{gazeHandIntervals.Quantile(qh):F0} ms)",
                $"  bids = {string.Join(' ', gazeHandIntervalBids)}",
                $"Glance duration:",
                $"  mean = {glanceDurationMean:F0} ms (SD = {glanceDurationStd:F0} ms)",
                $"  median = {glanceDurations.Median():F0} ms ({glanceDurations.Quantile(ql):F0}..{glanceDurations.Quantile(qh):F0} ms)",
                $"Pupil size",
                $"  mean = {pupilSizeMean:F2} (SD = {pupilSizeStd:F2})",
                $"  median = {processor.PupilSizes.Median():F2} ({processor.PupilSizes.Quantile(ql):F2}..{processor.PupilSizes.Quantile(qh):F2})",
                $"  calibrated mean = {calibratedPupilSizes.Mean():F2}",
                $"  calibrated median = {calibratedPupilSizes.Median():F2} ({calibratedPupilSizes.Quantile(ql):F2}..{calibratedPupilSizes.Quantile(qh):F2})",
                $"Gaze-lost events: {processor.GazeDataMisses.Length}",
                $"  blinks: {blinkCount} or {blinkCount2}",
                $"  eyes closed or lost: {longEyeLostCount}",
                $"Hand movement onset delay",
                $"  mean = {handMovementOnsetDelayMean:F0} ms (SD = {handMovementOnsetDelayStd:F1} ms)",
                $"Hand selection duration",
                $"  mean = {handSelectionDurationMean:F0} ms (SD = {handSelectionDurationStd:F1} ms)",
                $"First gaze movement onset delay",
                $"  mean = {firstGazeMovementOnsetDelayMean:F0} ms (SD = {firstGazeMovementOnsetDelayStd:F1} ms)",
                $"Selection fixation duration",
                $"  mean = {selectionFixationDurationMean:F0} ms (SD = {selectionFixationDurationStd:F1} ms)",
                $"Total trial gaze duration",
                $"  mean = {totalGazeDurationMean:F0} ms (SD = {totalGazeDurationStd:F1} ms)",
                $"Hand and gaze peak intersection time",
                $"  mean = {handGazeIntersectionMean:F0} ms (SD = {handGazeIntersectionStd:F1} ms)",
                $"  per trial = {handGazeIntersectionPerTrial:F0} ms",
                $"  count = {handGazeIntersections.Count()}",
                $"  valid trial count = {trialsWithHandPeak.Count()}",
            ]);
        else if (format == Format.Rows || format == Format.RowHeaders)
        {
            var matchBidsStr = matchBids.Select(v => v.ToString()).ToArray();
            var gazeHandIntervalBidsStr = gazeHandIntervalBids.Select(v => v.ToString()).ToArray();

            (string, object)[] rows = [
                ("Pace", processor.Vdl?.Condition.Pace ?? ""),
                ("Lambda", processor.Vdl?.Condition.Lambda ?? 0),
                ("Layout", processor.Vdl?.Condition.Layout ?? ""),
                ("Digits", processor.Vdl?.Condition.Digits ?? 0),

                ("Hand peaks", processor.HandPeaks.Length),
                ("Gaze peaks", processor.GazePeaks.Length),
                ("Peak matches, %", matchesCountPercentage),
                ($"{string.Join('\n', matchBids.Select((_, i) => $"Peak matches, bid {i+1}"))}",
                 $"{string.Join('\n', matchBidsStr)}"),

                ("Response time, mean", responseIntervalMean),
                ("Response time, SD", responseIntervalStd),
                ("Response time, median", responseIntervals.Any() ? responseIntervals.Median() : NODATA),
                ($"Response time, quantile {ql*100:F0}%", responseIntervals.Any() ? responseIntervals.Quantile(ql) : NODATA),
                ($"Response time, quantile {qh*100:F0}%", responseIntervals.Any() ? responseIntervals.Quantile(qh) : NODATA),

                ("Gaze-hand advance, mean", double.IsNaN(gazeHandIntervalMean) ? NODATA : gazeHandIntervalMean),
                ("Gaze-hand advance, SD", double.IsNaN(gazeHandIntervalStd) ? NODATA : gazeHandIntervalStd),
                ("Gaze-hand advance, median", double.IsNaN(gazeHandIntervalMedian) ? NODATA : gazeHandIntervalMedian),
                ($"Gaze-hand advance, quantile {ql*100:F0}%", double.IsNaN(gazeHandIntervalMean) ? NODATA : gazeHandIntervals.Quantile(ql)),
                ($"Gaze-hand advance, quantile {qh*100:F0}%", double.IsNaN(gazeHandIntervalMean) ? NODATA : gazeHandIntervals.Quantile(qh)),

                ($"{string.Join('\n', gazeHandIntervalBids.Select((_, i) => $"Gaze-hand advance, bid {i+1}"))}",
                 $"{string.Join('\n', gazeHandIntervalBids.Count() < 5 ? emptyBids : gazeHandIntervalBidsStr)}"),

                ("Fixation duration, mean", glanceDurationMean),
                ("Fixation duration, SD", glanceDurationStd),
                ("Fixation duration, median", glanceDurations.Median()),
                ($"Fixation duration, quantile {ql*100:F0}%", glanceDurations.Quantile(ql)),
                ($"Fixation duration, quantile {qh*100:F0}%", glanceDurations.Quantile(qh)),

                ("Pupil size, mean", pupilSizeMean),
                ("Pupil size, SD", pupilSizeStd),
                ("Pupil size, median", processor.PupilSizes.Median()),
                ($"Pupil size, quantile {ql*100:F0}%", processor.PupilSizes.Quantile(ql)),
                ($"Pupil size, quantile {qh*100:F0}%", processor.PupilSizes.Quantile(qh)),

                ("Eye losses", processor.GazeDataMisses.Length),
                ("Blinks", blinkCount),
                ("Long eye losses", longEyeLostCount),
                ("Correct responses, %", 100*correctResponses),
                ("Calibrated pupil size, mean", pupilSizeMean - (processor.Vdl?.PupilCalibration?.Size ?? 0)),
                ("Blinks 2", blinkCount2),

                ("Hand movement onset, mean", handMovementOnsetDelayMean),
                ("Hand movement onset, SD", handMovementOnsetDelayStd),
                ("Hand selection duration, mean", handSelectionDurationMean),
                ("Hand selection duration, SD", handSelectionDurationStd),
                ("First gaze movement onset, mean", firstGazeMovementOnsetDelayMean),
                ("First gaze movement onset, SD", firstGazeMovementOnsetDelayStd),
                ("Selection fixation duration, mean", selectionFixationDurationMean),
                ("Selection fixation duration, SD", selectionFixationDurationStd),
                ("Total trial gaze duration, mean", totalGazeDurationMean),
                ("Total trial gaze duration, SD", totalGazeDurationStd),
                ("Gaze-Hand match count", gazeHandMatchCount),
                ("Gaze-preceides-Hand count", gazeFollowsHandCount),

                ("Gaze-Hand peak union time, mean", handGazeIntersectionMean),
                ("Gaze-Hand peak union time, per trial", handGazeIntersectionPerTrial),
                ("Gaze-Hand peak unions", handGazeIntersections.Count()),
                ("Trial count", trialsWithHandPeak.Count()),
            ];
            return string.Join('\n', format == Format.RowHeaders ?
                rows.Select(row => row.Item1) :
                rows.Select(row => row.Item2));
        }

        return "";
    }


    // Internal

    const string NODATA = ".";
}
