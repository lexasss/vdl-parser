using System.Text.Json;

namespace VdlParser;

public record class GazeDataMiss(
    long StartTimestamp, int StartIndex, 
    long EndTimestamp, int EndIndex,
    long Duration, bool IsBlink, bool IsLong);

public class BlinkDetector
{
    public int MinGazeLostInterval { get; set; } = 40; // ms
    public int BlinkMinDuration { get; set; } = 120; // ms
    public int BlinkMaxDuration { get; set; } = 350; // ms
    public int MergeInterval { get; set; } = 100; // ms
    public double BlinkMaxLevelDifference { get; set; } = 6;
    public int LevelDifferenceBufferSize { get; set; } = 3;

    public static BlinkDetector Load()
    {
        var settings = Properties.Settings.Default;
        string json = settings.BlinkDetector;

        var defaultDetector = new BlinkDetector();

        if (string.IsNullOrEmpty(json))
        {
            return defaultDetector;
        }

        BlinkDetector? result = null;
        try
        {
            result = JsonSerializer.Deserialize<BlinkDetector>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }

        return result ?? defaultDetector;
    }

    public static void Save(BlinkDetector detector)
    {
        var json = JsonSerializer.Serialize(detector);

        var settings = Properties.Settings.Default;
        settings.BlinkDetector = json;
        settings.Save();
    }

    public GazeDataMiss[] Find(Sample[] samples)
    {
        var misses = new List<GazeDataMiss>();

        int index = 0;
        int lastTimestampIndex = 0;

        long lastTimestamp = 0;
        long lastBlinkEndTimestamp = 0;
        foreach (Sample sample in samples)
        {
            if (lastTimestamp > 0)
            {
                var interval = sample.Timestamp - lastTimestamp;
                if (interval > MinGazeLostInterval)
                {
                    if (lastBlinkEndTimestamp > 0 && (lastTimestamp - lastBlinkEndTimestamp) < MergeInterval)
                    {
                        var missToBeReplaced = misses[^1];
                        interval = sample.Timestamp - missToBeReplaced.StartTimestamp;
                        misses[^1] = new GazeDataMiss(
                            missToBeReplaced.StartTimestamp, missToBeReplaced.StartIndex,
                            sample.Timestamp, index, interval,
                            interval >= BlinkMinDuration && interval <= BlinkMaxDuration,
                            interval > BlinkMaxDuration);
                    }
                    else
                    {
                        misses.Add(new GazeDataMiss(
                            lastTimestamp, lastTimestampIndex,
                            sample.Timestamp, index, interval,
                            interval >= BlinkMinDuration && interval <= BlinkMaxDuration,
                            interval > BlinkMaxDuration));
                    }

                    lastBlinkEndTimestamp = sample.Timestamp;
                }
            }

            lastTimestamp = sample.Timestamp;
            lastTimestampIndex = index;

            index += 1;
        }

        var result = misses.ToArray();
        ImproveGazeMissClassification(samples, result);

        return result;
    }

    // Internal

    private void ImproveGazeMissClassification(Sample[] samples, GazeDataMiss[] misses)
    {
        double GetMean(int index, int direction, int size)
        {
            double sum = 0;
            int count = 0;
            while (index >= 0 && index < samples.Length && count < size)
            {
                var sample = samples[index];
                sum += sample.Value;
                count += 1;
                index += direction;
            }

            return sum / (count > 0 ? count : 1);
        }

        for (int i = 0; i < misses.Length; i++)
        {
            var miss = misses[i];
            var startValue = GetMean(miss.StartIndex, -1, LevelDifferenceBufferSize);
            var endValue = GetMean(miss.EndIndex, 1, LevelDifferenceBufferSize);

            if (Math.Abs(startValue - endValue) > BlinkMaxLevelDifference)
            {
                misses[i] = miss with { IsBlink = false };
            }
        }
    }
}