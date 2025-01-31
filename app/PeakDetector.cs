using MathNet.Numerics.Statistics;
using System.ComponentModel;
using System.Text.Json;

namespace VdlParser;

public record class Sample(long Timestamp, double Value);
public record class Peak(int StartIndex, long TimestampStart, long TimestampEnd, double Amplitude);

public enum DataSourceType
{
    Hand,
    Gaze
}

public enum PeakDirection
{
    Upward,
    Downward
}

public class PeakDetector : INotifyPropertyChanged
{
    public int BufferSize
    {
        get => _bufferSize;
        set => _bufferSize = Math.Max(3, value);
    }

    public double PeakThreshold { get; set; } = 1.5;
    public double IgnoranceThrehold { get; set; } = 20;
    public long MaxPeakDuration { get; set; } = 1500;   // ms
    public long MinInterPeakInterval { get; set; } = 1000;   // ms
    public PeakDirection Direction { get; set; } = PeakDirection.Upward;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static PeakDetector Load(DataSourceType dataSourceType)
    {
        var settings = Properties.Settings.Default;
        string json = dataSourceType switch
        {
            DataSourceType.Hand => settings.HandPeakDetector,
            DataSourceType.Gaze => settings.GazePeakDetector,
            _ => throw new NotSupportedException($"{dataSourceType} is not yet suppoted")
        };

        var defaultDetector = dataSourceType switch
        {
            DataSourceType.Hand => new PeakDetector() { PeakThreshold = 1.5, BufferSize = 12, IgnoranceThrehold = 20 },
            DataSourceType.Gaze => new PeakDetector() { PeakThreshold = 10, BufferSize = 30, IgnoranceThrehold = -1000 },
            _ => throw new NotSupportedException($"{dataSourceType} is not yet suppoted")
        };

        if (string.IsNullOrEmpty(json))
        {
            return defaultDetector;
        }

        PeakDetector? result = null;
        try
        {
            result = JsonSerializer.Deserialize<PeakDetector>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }

        return result ?? defaultDetector;
    }

    public static void Save(DataSourceType dataSourceType, PeakDetector detector)
    {
        var json = JsonSerializer.Serialize(detector);

        var settings = Properties.Settings.Default;
        if (dataSourceType == DataSourceType.Hand)
        {
            settings.HandPeakDetector = json;
        }
        else if (dataSourceType == DataSourceType.Gaze)
        {
            settings.GazePeakDetector = json;
        }

        settings.Save();
    }

    public Peak[] Find(Sample[] samples)
    {
        var peaks = new List<Peak>();
        var ignoranceThreshold = Direction switch
        {
            PeakDirection.Upward => IgnoranceThrehold,
            PeakDirection.Downward => -IgnoranceThrehold,
            _ => throw new NotImplementedException($"{Direction} direction is not supported")
        };

        int i = 0;

        bool isInPeak = false;
        long timestampStart = 0;
        long timestampLastPeakEnd = 0;
        int timestampStartIndex = 0;

        // Advances "i" until the last "BufferSize" samples stay outside of the ignorance zone,
        // which is limited by "IgnoranceThrehold" (depend on "Direction" whether it limits
        // from the top or from the bottom)
        // Returns "false" if no datapoints left to analyze
        bool SeekBufferHead()
        {
            var firstValidDatapointIndex = i;

            while (firstValidDatapointIndex < samples.Length && IsBelowThreshold(samples[firstValidDatapointIndex].Value, ignoranceThreshold))
            {
                firstValidDatapointIndex += 1;
            }

            i = firstValidDatapointIndex;
            for (int j = i + 1; (i + j) < samples.Length && j < BufferSize; j++)
            {
                if (IsBelowThreshold(samples[++i].Value, ignoranceThreshold))
                {
                    return SeekBufferHead();
                }
            }

            return i < samples.Length;
        }

        if (!SeekBufferHead())
            return peaks.ToArray();

        while (++i < samples.Length)
        {
            if (IsBelowThreshold(samples[i].Value, ignoranceThreshold) && !SeekBufferHead())
                break;

            var chunk = samples[(i - BufferSize)..i];
            var (avg1, avg2) = GetAverages(chunk);
            var difference = avg2 - avg1;

            var timestampCurrent = chunk[_bufferSize / 2].Timestamp;
            var timeElapsedSinceTheLastPeak = timestampLastPeakEnd > 0 ? (timestampCurrent - timestampLastPeakEnd) : long.MaxValue;

            if (!isInPeak && IsAboveThreshold(difference, PeakThreshold) && timeElapsedSinceTheLastPeak > MinInterPeakInterval)
            {
                isInPeak = true;

                timestampStart = timestampCurrent;
                timestampStartIndex = i - _bufferSize / 2;
            }
            else if (isInPeak && IsBelowThreshold(difference, -PeakThreshold))
            {
                isInPeak = false;

                if (timestampStart != 0)
                {
                    var timestampEnd = timestampCurrent;
                    timestampLastPeakEnd = timestampEnd;

                    if ((timestampEnd - timestampStart) < MaxPeakDuration)
                    {
                        var peakValue = samples[timestampStartIndex..i].Select(s => s.Value).Median();
                        peaks.Add(new Peak(timestampStartIndex, timestampStart, timestampEnd, peakValue));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[{timestampStartIndex}] {timestampStart}-{timestampEnd} ({timestampEnd - timestampStart}) is too long interval");
                    }
                }

                timestampStart = 0;
            }
        }

        return peaks.ToArray();
    }

    public void ReversePeakSearchDirection()
    {
        PeakThreshold = -PeakThreshold;
        Direction = Direction == PeakDirection.Upward ? PeakDirection.Downward : PeakDirection.Upward;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PeakThreshold)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Direction)));
    }

    // Internal

    int _bufferSize = 12;

    private (double, double) GetAverages(Sample[] samples)
    {
        int count1 = 0;
        int count2 = 0;
        double sum1 = 0;
        double sum2 = 0;

        int center = _bufferSize / 2;
        for (int i = 0; i < samples.Length; i++)
        {
            if (i < center)
            {
                count1++;
                sum1 += samples[i].Value;
            }
            else
            {
                count2++;
                sum2 += samples[i].Value;
            }
        }

        return (sum1 / count1, sum2 / count2);
    }

    private bool IsBelowThreshold(double value, double threshold) => Direction switch
    {
        PeakDirection.Upward => value < threshold,
        PeakDirection.Downward => value > threshold,
        _ => throw new NotImplementedException($"{Direction} direction is not supported")
    };

    private bool IsAboveThreshold(double value, double threshold) => Direction switch
    {
        PeakDirection.Upward => value > threshold,
        PeakDirection.Downward => value < threshold,
        _ => throw new NotImplementedException($"{Direction} direction is not supported")
    };
}
