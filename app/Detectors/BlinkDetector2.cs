using System.Text.Json;

namespace VdlParser.Detectors;

public record class Blink(
    long StartTimestamp, int StartIndex,
    long EndTimestamp, int EndIndex,
    long Duration);

public class BlinkDetector2
{
    public int BlinkMinDuration { get; set; } = 40; // ms
    public int BlinkMaxDuration { get; set; } = 350; // ms
    public int BufferSize { get; set; } = 5;

    public static BlinkDetector2 Load()
    {
        var settings = Properties.Settings.Default;
        string json = settings.BlinkDetector2;

        var defaultDetector = new BlinkDetector2();

        if (string.IsNullOrEmpty(json))
        {
            return defaultDetector;
        }

        BlinkDetector2? result = null;
        try
        {
            result = JsonSerializer.Deserialize<BlinkDetector2>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }

        return result ?? defaultDetector;
    }

    public static void Save(BlinkDetector2 detector)
    {
        var json = JsonSerializer.Serialize(detector);

        var settings = Properties.Settings.Default;
        settings.BlinkDetector2 = json;
        settings.Save();
    }

    public Blink[] Find(VdlRecord[] samples)
    {
        double PeakStrength(int index, double thresoholdLeft, double thresoholdRight, Func<VdlRecord, double> getData)
        {
            if ((index + BufferSize) >= samples.Length)
                return 0;

            var diffLeft = getData(samples[index - 1]) - getData(samples[index - BufferSize]);
            var diffRight = getData(samples[index]) - getData(samples[index + BufferSize - 1]);
            var a = diffLeft / thresoholdLeft * 1.75;
            var b = diffRight / thresoholdRight * 1.75;
            return Math.Max(0, Math.Min(a / Math.Sqrt(1 + a * a), b / Math.Sqrt(1 + b * b)));
        }

        var timestampSource = Settings.Instance.TimestampSource;
        var gazeDataSource = Settings.Instance.GazeDataSource;

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
            if (interval > BlinkMinDuration)
            {
                var peakInGazeData = PeakStrength(i, -0.5, -2, GetEyeData);
                var peakInPupilSize = PeakStrength(i, -0.1, -0.1, r => (r.LeftPupil.Size + r.RightPupil.Size) / 2);
                var peakInPupilOpenness = PeakStrength(i, -0.15, -0.15, r => (r.LeftPupil.Openness + r.RightPupil.Openness) / 2);

                var thresholds = new double[] { peakInGazeData, peakInPupilSize, peakInPupilOpenness };
                if (thresholds.All(t => t > 0.4))
                {
                    peakInGazeData = PeakStrength(i, -0.5, -2, GetEyeData);
                    peakInPupilSize = PeakStrength(i, -0.1, -0.1, r => (r.LeftPupil.Size + r.RightPupil.Size) / 2);
                    peakInPupilOpenness = PeakStrength(i, -0.15, -0.15, r => (r.LeftPupil.Openness + r.RightPupil.Openness) / 2);
                    System.Diagnostics.Debug.WriteLine($"[{i}] {ts} > {peakInGazeData} * {peakInPupilSize} * {peakInPupilOpenness}");
                    blinks.Add(new Blink(lastTimestamp, i - 1, ts, i, interval));

                    i += BufferSize;
                }
            }

            lastTimestamp = ts;
        }

        return blinks.ToArray();
    }
}