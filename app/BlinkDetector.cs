using System.Text.Json;

namespace VdlParser;

public record class GazeDataMiss(long TimestampStart, long TimestampEnd, long Duration, bool IsBlink);

public class BlinkDetector
{
    public int BlinkMinDuration { get; set; } = 120; // ms
    public int BlinkMaxDuration { get; set; } = 350; // ms

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

        long lastTimestamp = 0;
        foreach (Sample sample in samples)
        {
            if (lastTimestamp > 0)
            {
                var interval = sample.Timestamp - lastTimestamp;
                if (interval > 30)
                {
                    misses.Add(new GazeDataMiss(lastTimestamp, sample.Timestamp, interval,
                        interval >= BlinkMinDuration && interval <= BlinkMaxDuration));
                }
            }

            lastTimestamp = sample.Timestamp;
        }

        return misses.ToArray();
    }
}