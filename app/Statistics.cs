using MathNet.Numerics.Statistics;

namespace VdlParser;

public class Statistics(Processor processor)
{
    public double QuantileLow { get; set; } = 0.1;
    public double QuantileHigh { get; set; } = 0.9;

    public string GetAsText()
    {
        var matchesCountPercentage = processor.HandPeaks.Length > 0 ?
            100 * processor.Trials.Length / processor.HandPeaks.Length : 0;
        var responseIntervals = processor.Trials
            .Where(trial => trial.TimestampResponse > 0)
            .Select(trial => (double)(trial.TimestampStart - trial.TimestampResponse));
        var (responseIntervalMean, responseIntervalStd) = responseIntervals.MeanStandardDeviation();
        var gazeHandIntervals = processor.Trials.Select(trial => (double)trial.GazeHandInterval);
        var (gazeHandIntervalMean, gazeHandIntervalStd) = gazeHandIntervals.MeanStandardDeviation();
        var glanceDurations = processor.GazePeaks.Select(peak => (double)(peak.TimestampEnd - peak.TimestampStart));
        var (glanceDurationMean, glanceDurationStd) = glanceDurations.MeanStandardDeviation();
        var (pupilSizeMean, pupilSizeStd) = processor.PupilSizes.MeanStandardDeviation();
        var longEyeLostCount = processor.GazeDataMisses
            .Where(gdm => gdm.IsLong)
            .Count();

        return string.Join('\n', [
            $"Hand/Gaze peaks: {processor.HandPeaks.Length}/{processor.GazePeaks.Length}",
            $"  match count = {processor.Trials.Length} ({matchesCountPercentage:F1}%)",
            $"Response delay",
            $"  mean = {responseIntervalMean:F0} ms (SD = {responseIntervalStd:F1} ms)",
            $"  median = {responseIntervals.Median():F0} ms ({responseIntervals.Quantile(QuantileLow):F0}..{responseIntervals.Quantile(QuantileHigh):F0} ms)",
            $"Gaze delay",
            $"  mean = {gazeHandIntervalMean:F0} ms (SD = {gazeHandIntervalStd:F1} ms)",
            $"  median = {gazeHandIntervals.Median():F0} ms ({gazeHandIntervals.Quantile(QuantileLow):F0}..{gazeHandIntervals.Quantile(QuantileHigh):F0} ms)",
            $"Glance duration:",
            $"  mean = {glanceDurationMean:F0} ms (SD = {glanceDurationStd:F0} ms)",
            $"  median = {glanceDurations.Median():F0} ms ({glanceDurations.Quantile(QuantileLow):F0}..{glanceDurations.Quantile(QuantileHigh):F0} ms)",
            $"Pupil size",
            $"  mean = {pupilSizeMean:F2} (SD = {pupilSizeStd:F2})",
            $"  median = {processor.PupilSizes.Median():F2} ({processor.PupilSizes.Quantile(QuantileLow):F2}..{processor.PupilSizes.Quantile(QuantileHigh):F2})",
            $"Gaze-lost events: {processor.GazeDataMisses.Length}",
            $"  blinks: {processor.GazeDataMisses.Where(gdm => gdm.IsBlink).Count()}",
            $"  eyes closed or lost: {longEyeLostCount}",
        ]);
    }
}
