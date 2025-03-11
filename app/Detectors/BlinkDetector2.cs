using VdlParser.Models;

namespace VdlParser.Detectors;

public record class Blink(
    long StartTimestamp, int StartIndex,
    long EndTimestamp, int EndIndex,
    long Duration);

public class BlinkDetector2 : ISettings
{
    public int BlinkMinDuration { get; set; } = 40; // ms
    public int BlinkMaxDuration { get; set; } = 350; // ms
    public int BufferSize { get; set; } = 4;
    public double ThresholdEyeRotation { get; set; } = -1.5;
    public double ThresholdPupilOpenness { get; set; } = -0.1;
    public double ThresholdPupilSize { get; set; } = -0.1;
    public double ThresholdConfidence { get; set; } = 0.5;

    public string Section => nameof(BlinkDetector2);

    public Blink[] Find(VdlRecord[] samples)
    {
        /*
        double GetPeakConfidence(int index, double thresohold, Func<VdlRecord, double> getData, bool ignoreAfterPeak = false)
        {
            if ((index + BufferSize) >= samples.Length)
                return 0;

            var buffer = samples[(index - BufferSize)..(index + BufferSize)].Select(s => getData(s));

            var start = buffer.Take(2).Mean();
            var end = ignoreAfterPeak ? start : buffer.TakeLast(2).Mean();
            var baseline = (start + end) / 2;
            var min = buffer.Skip(2).Take(2 * BufferSize - 4).Min();

            if (Math.Abs(start - end) > Math.Abs((baseline - min) * 0.5) ||
                Math.Abs(start - min) < Math.Abs(thresohold) || 
                (ignoreAfterPeak ? false : Math.Abs(end - min) < Math.Abs(thresohold)))
                return 0;

            var a = (min - baseline) / thresohold * 1.75;
            return Math.Max(0, a / Math.Sqrt(1 + a * a));
        }*/
        double GetPeakConfidence(int index, double thresohold, Func<VdlRecord, double> getData, bool ignoreRight = false)
        {
            if ((index + BufferSize) >= samples.Length)
                return 0;

            var currentValue = getData(samples[index]);
            var diffLeft = currentValue - getData(samples[index - BufferSize]);
            var diffRight = ignoreRight ? diffLeft : currentValue - getData(samples[index + BufferSize]);

            var diff = (diffLeft + diffRight) / 2;
            var a = diff / thresohold;
            var result = Math.Max(0, a / Math.Sqrt(1 + a * a));

            if (diffLeft / diffRight < 0.3 || diffLeft / diffRight > 3)
                return Math.Min(0.1, result);

            return result;
        }

        var timestampSource = GeneralSettings.Instance.TimestampSource;
        var gazeDataSource = GeneralSettings.Instance.GazeDataSource;

        long GetTimestamp(VdlRecord record) => timestampSource switch
        {
            TimestampSource.Headset => record.TimestampHeadset,
            TimestampSource.System => record.TimestampSystem,
            _ => throw new NotSupportedException($"{timestampSource} timestamp source is not supported"),
        };
        double GetEyeData(VdlRecord record) => gazeDataSource switch
        {
            GazeDataSource.YawRotation => record.Eye.Yaw,
            GazeDataSource.PitchRotation => record.Eye.Pitch,
            _ => throw new NotSupportedException($"{timestampSource} eye data source is not supported"),
        };

        var blinks = new List<Blink>();

        long lastTimestamp = GetTimestamp(samples[BufferSize - 1]);

        for (int i = BufferSize; i < samples.Length; i++)
        {
            var sample = samples[i];
            var ts = GetTimestamp(sample);
            var interval = ts - lastTimestamp;
            if (interval >= BlinkMinDuration && interval <= BlinkMaxDuration)
            {
                var confOfPeakInGazeData = GetPeakConfidence(i, ThresholdEyeRotation, GetEyeData);
                var confOfPeakInPupilSize = GetPeakConfidence(i, ThresholdPupilSize, r => r.PupilSize);
                var confOfPeakInPupilOpenness = GetPeakConfidence(i, ThresholdPupilOpenness, r => r.PupilOpenness, ignoreRight: true);

                var debugMsg = "--------";
                var confidences = new double[] { confOfPeakInGazeData, confOfPeakInPupilSize, confOfPeakInPupilOpenness };
                if (confidences.All(t => t > ThresholdConfidence))
                {
                    /*
                    confOfPeakInGazeData = GetPeakConfidence(i, ThresholdEyeRotation, GetEyeData);
                    confOfPeakInPupilSize = GetPeakConfidence(i, ThresholdPupilSize, r => r.PupilSize);
                    confOfPeakInPupilOpenness = GetPeakConfidence(i, ThresholdPupilOpenness, r => r.PupilOpenness, ignoreRight: true);
                    */
                    blinks.Add(new Blink(lastTimestamp, i - 1, ts, i, interval));

                    i += BufferSize;
                    debugMsg = ">> blink";
                }

                System.Diagnostics.Debug.WriteLine($"[{i}] {ts} > Gap {interval} ms {debugMsg} {confOfPeakInGazeData:F3} * {confOfPeakInPupilSize:F3} * {confOfPeakInPupilOpenness:F3}");
            }

            lastTimestamp = ts;
        }

        return blinks.ToArray();
    }
}