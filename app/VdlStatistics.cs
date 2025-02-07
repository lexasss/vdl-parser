using MathNet.Numerics.Statistics;

namespace VdlParser;

public enum StatisticsFormat
{
    List,
    Rows,
    RowHeaders
}

public class VdlStatistics(Processor processor)
{
    public double QuantileThreshold { get; set; } = 0.1;

    public string Get(StatisticsFormat format)
    {
        var gazeHandMatchCount = processor.Trials
            .Where(trial => trial.HasHandGazeMatch)
            .Count();
        var matchesCountPercentage = 100.0 * gazeHandMatchCount / processor.Trials.Count();
        var responseIntervals = processor.Trials
            .Where(trial => trial.ResponseTimestamp > 0)
            .Select(trial => (double)(trial.ResponseTimestamp - trial.StartTimestamp));
        var (responseIntervalMean, responseIntervalStd) = responseIntervals.MeanStandardDeviation();
        var gazeHandIntervals = processor.Trials
            .Where(trial => trial.HasHandGazeMatch)
            .Select(trial => (double)trial.GazeHandInterval);
        var (gazeHandIntervalMean, gazeHandIntervalStd) = gazeHandIntervals.MeanStandardDeviation();
        var glanceDurations = processor.GazePeaks.Select(peak => (double)(peak.TimestampEnd - peak.TimestampStart));
        var (glanceDurationMean, glanceDurationStd) = glanceDurations.MeanStandardDeviation();
        var (pupilSizeMean, pupilSizeStd) = processor.PupilSizes.MeanStandardDeviation();
        var blinkCount = processor.GazeDataMisses
            .Where(gdm => gdm.IsBlink)
            .Count();
        var longEyeLostCount = processor.GazeDataMisses
            .Where(gdm => gdm.IsLong)
            .Count();
        var tb = new TemproralBids();
        var gazeHandIntervalBids = tb.Get(processor.Trials
                .Where(trial => trial.HasHandGazeMatch)
                .Select(trial => new Timestamped(trial.StartTimestamp, trial.GazeHandInterval))
                .ToArray())
            .Select(bid => Math.Round(bid));
        var correctResponses = (double)processor.Trials.Sum(trial => trial.IsCorrect ? 1 : 0) / processor.Trials.Length;

        var ql = QuantileThreshold;
        var qh = 1.0 - QuantileThreshold;

        if (format == StatisticsFormat.List)
            return string.Join('\n', [
                $"Hand/Gaze peaks: {processor.HandPeaks.Length}/{processor.GazePeaks.Length}",
                $"  match count = {processor.Trials.Length} ({matchesCountPercentage:F1}%)",
                $"Correct responses = {correctResponses*100:F1}%",
                $"Response delay",
                $"  mean = {responseIntervalMean:F0} ms (SD = {responseIntervalStd:F1} ms)",
                $"  median = {responseIntervals.Median():F0} ms ({responseIntervals.Quantile(ql):F0}..{responseIntervals.Quantile(qh):F0} ms)",
                $"Gaze delay",
                $"  mean = {gazeHandIntervalMean:F0} ms (SD = {gazeHandIntervalStd:F1} ms)",
                $"  median = {gazeHandIntervals.Median():F0} ms ({gazeHandIntervals.Quantile(ql):F0}..{gazeHandIntervals.Quantile(qh):F0} ms)",
                $"  bids = {string.Join(' ', gazeHandIntervalBids)}",
                $"Glance duration:",
                $"  mean = {glanceDurationMean:F0} ms (SD = {glanceDurationStd:F0} ms)",
                $"  median = {glanceDurations.Median():F0} ms ({glanceDurations.Quantile(ql):F0}..{glanceDurations.Quantile(qh):F0} ms)",
                $"Pupil size",
                $"  mean = {pupilSizeMean:F2} (SD = {pupilSizeStd:F2})",
                $"  median = {processor.PupilSizes.Median():F2} ({processor.PupilSizes.Quantile(ql):F2}..{processor.PupilSizes.Quantile(qh):F2})",
                $"Gaze-lost events: {processor.GazeDataMisses.Length}",
                $"  blinks: {blinkCount}",
                $"  eyes closed or lost: {longEyeLostCount}",
            ]);
        else if (format == StatisticsFormat.Rows || format == StatisticsFormat.RowHeaders)
        {
            (string, object)[] rows = [
                ("Hand peaks", processor.HandPeaks.Length),
                ("Gaze peaks", processor.GazePeaks.Length),
                ("Peak matches, %", matchesCountPercentage),
                ("Response duration, mean", responseIntervalMean),
                ("Response duration, SD", responseIntervalStd),
                ("Response duration, median", responseIntervals.Median()),
                ($"Response duration, quantile {ql*100:F0}%", responseIntervals.Quantile(ql)),
                ($"Response duration, quantile {qh*100:F0}%", responseIntervals.Quantile(qh)),
                ("Gaze-hand delay, mean", gazeHandIntervalMean),
                ("Gaze-hand delay, SD", gazeHandIntervalStd),
                ("Gaze-hand delay, median", gazeHandIntervals.Median()),
                ($"Gaze-hand delay, quantile {ql*100:F0}%", gazeHandIntervals.Quantile(ql)),
                ($"Gaze-hand delay, quantile {qh*100:F0}%", gazeHandIntervals.Quantile(qh)),
                ($"{string.Join('\n', gazeHandIntervalBids.Select((_, i) => $"bid {i+1}"))}",
                 $"{string.Join('\n', gazeHandIntervalBids)}"),
                ("Glance duration, mean", glanceDurationMean),
                ("Glance duration, SD", glanceDurationStd),
                ("Glance duration, median", glanceDurations.Median()),
                ($"Glance duration, quantile {ql*100:F0}%", glanceDurations.Quantile(ql)),
                ($"Glance duration, quantile {qh*100:F0}%", glanceDurations.Quantile(qh)),
                ("Pupil size, mean", pupilSizeMean),
                ("Pupil size, SD", pupilSizeStd),
                ("Pupil size, median", processor.PupilSizes.Median()),
                ($"Pupil size, quantile {ql*100:F0}%", processor.PupilSizes.Quantile(ql)),
                ($"Pupil size, quantile {qh*100:F0}%", processor.PupilSizes.Quantile(qh)),
                ("Eye losses", processor.GazeDataMisses.Length),
                ("Blinks", blinkCount),
                ("Long eye losses", longEyeLostCount),
                ("Correct responses, %", 100*correctResponses),
            ];
            return string.Join('\n', format == StatisticsFormat.RowHeaders ?
                rows.Select(row => row.Item1) :
                rows.Select(row => row.Item2));
        }

        return "";
    }
}
