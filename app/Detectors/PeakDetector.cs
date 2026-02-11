using MathNet.Numerics.Statistics;
using System.ComponentModel;

namespace VdlParser.Detectors;

public record class Sample(long Timestamp, double Value);
public record class Peak(int StartIndex, long TimestampStart, long TimestampEnd, double Amplitude)
{
   public long Duration => TimestampEnd - TimestampStart;
};

public enum PeakDirection
{
    Up,
    Down
}

[TypeConverter(typeof(FriendlyEnumConverter))]
public enum HandPeakDetectionMethod
{
    RealTime,
    ResponseBased
}

public class HandPeakDetector : PeakDetector, ISettings
{
    public string Section => nameof(HandPeakDetector);
    public double MinPeakHeight { get; set; } = 5;
    public HandPeakDetectionMethod HandPeakDetectionMethod { get; set; } = HandPeakDetectionMethod.ResponseBased;

    public HandPeakDetector() : base()
    {
        MinPeakDuration = 500;
    }

    public Peak[] Find(Sample[] samples, long startTimestamp, TimestampedNbtEvent[] responses) => HandPeakDetectionMethod switch
    {
        HandPeakDetectionMethod.RealTime => base.Find(samples, startTimestamp),
        HandPeakDetectionMethod.ResponseBased => FindForResponses(samples, responses),
        _ => throw new NotImplementedException($"{HandPeakDetectionMethod} is not supported")
    };

    // Internal

    private Peak[] FindForResponses(Sample[] samples, TimestampedNbtEvent[] responses)
    {
        var peaks = new List<Peak>();
        if (samples.Length == 0)
            return peaks.ToArray();

        var responseTimestamps = responses.Select(r => r.Timestamp).ToHashSet();
        //samples = samples.Where(s => s.Value != 0).ToArray();

        int ri = 0;
        foreach (var responseTimestamp in responseTimestamps)
        {
            Sample responseSample = samples[ri];
            if (responseSample.Timestamp > responseTimestamp)
                continue;

            // Initially, find the sample at or just after the response timestamp
            while (responseSample.Timestamp < responseTimestamp && ri < samples.Length - 1)
            {
                responseSample = samples[++ri];
            }

            if (ri == samples.Length || Math.Abs(responseSample.Timestamp - responseTimestamp) > 250)   // ms, tolerance
                continue;

            int si = ri - 1;
            var sample = samples[si];

            // then, find the sample that drop below the threshold before the peak
            while (si > 0 && responseSample.Value - sample.Value < MinPeakHeight)
            {
                sample = samples[--si];
            }

            // finally, find the peak start
            si += _bufferSize / 2;
            var std = double.MaxValue;

            while (std > PeakThreshold && --si > _bufferSize)
            {
                var chunk = samples[(si - _bufferSize)..si];
                var validValue = chunk.LastOrDefault(sample => sample.Value != 0)?.Value ?? 0;
                std = chunk.Reverse().Select(s =>
                {
                    if (s.Value != 0)
                        validValue = s.Value;
                    return validValue;
                }).StandardDeviation();
            }

            if (si <= _bufferSize)
                continue;

            si -= _bufferSize / 3;

            int ei = ri + _bufferSize - 1;
            std = double.MaxValue;

            // then, find the sample that drop below the threshold after the peak
            while (std > PeakThreshold && ++ei < samples.Length)
            {
                var chunk = samples[(ei - _bufferSize)..ei];
                var validValue = chunk.FirstOrDefault(sample => sample.Value != 0)?.Value ?? 0;
                std = chunk.Select(s =>
                {
                    if (s.Value != 0)
                        validValue = s.Value;
                    return validValue;
                }).StandardDeviation();
            }

            ei -= _bufferSize / 3 * 2;

            var startTimestamp = samples[si].Timestamp;
            var endTimestamp = samples[ei].Timestamp;

            if (endTimestamp - startTimestamp > MaxPeakDuration)
                continue;

            peaks.Add(new Peak(si, startTimestamp, endTimestamp, samples[ri].Value - samples[si].Value));
        }

        return peaks.ToArray();
    }
}

public class GazePeakDetector : PeakDetector, ISettings
{
    public string Section => nameof(GazePeakDetector);
}

public class PeakDetector : INotifyPropertyChanged
{
    public int BufferSize
    {
        get => _bufferSize;
        set => _bufferSize = Math.Max(3, value);
    }

    public double PeakThreshold { get; set; } = 1.5;
    public long MinPeakDuration { get; set; } = 150;   // ms
    public long MaxPeakDuration { get; set; } = 1500;   // ms
    public long MinInterPeakInterval { get; set; } = 1000;   // ms
    public PeakDirection Direction { get; set; } = PeakDirection.Up;

    public event PropertyChangedEventHandler? PropertyChanged;

    public virtual Peak[] Find(Sample[] samples, long startTimestamp)
    {
        var peaks = new List<Peak>();

        int i = 0;

        bool isInPeak = false;
        long timestampStart = 0;
        long timestampLastPeakEnd = 0;
        int timestampStartIndex = 0;

        bool IsInvalidValue(double value) => value == 0;

        // Advances "i" until the last "BufferSize" samples all become valid.
        // Returns "false" if no valid datapoints left.
        bool SeekBufferHead()
        {
            var firstValidDatapointIndex = i;

            while (firstValidDatapointIndex < samples.Length &&
                IsInvalidValue(samples[firstValidDatapointIndex].Value))
            {
                firstValidDatapointIndex += 1;
            }

            i = firstValidDatapointIndex;
            for (int j = i + 1; i + j < samples.Length && j < BufferSize; j++)
            {
                if (IsInvalidValue(samples[++i].Value))
                {
                    return SeekBufferHead();
                }
            }

            return i < samples.Length;
        }

        if (!SeekBufferHead())
            return peaks.ToArray();

        while (i < samples.Length && samples[i].Timestamp < startTimestamp)
        {
            i++;
        }

        while (++i < samples.Length)
        {
            if (IsInvalidValue(samples[i].Value) && !SeekBufferHead())
                break;

            var chunk = samples[(i - BufferSize)..i];
            var (avg1, avg2) = GetAverages(chunk);
            var difference = avg2 - avg1;

            var timestampCurrent = chunk[_bufferSize / 2].Timestamp;
            var timeElapsedSinceTheLastPeak = timestampLastPeakEnd > 0
                ? timestampCurrent - timestampLastPeakEnd
                : long.MaxValue;

            if (!isInPeak && IsAboveThreshold(difference, PeakThreshold) &&
                timeElapsedSinceTheLastPeak > MinInterPeakInterval)
            {
                isInPeak = true;

                timestampStart = timestampCurrent;
                timestampStartIndex = i - _bufferSize / 2;
            }
            else if (isInPeak)
            {
                var timestampEnd = timestampCurrent;
                var duration = timestampEnd - timestampStart;

                if (duration > MaxPeakDuration)
                {
                    isInPeak = false;
                    timestampStart = 0;
                }
                else if (IsBelowThreshold(difference, -PeakThreshold))
                {
                    isInPeak = false;

                    if (timestampStart != 0)
                    {
                        timestampLastPeakEnd = timestampEnd;

                        if (duration < MaxPeakDuration && duration > MinPeakDuration)
                        {
                            var peakValue = samples[timestampStartIndex..i].Select(s => s.Value).Median();
                            peaks.Add(new Peak(timestampStartIndex, timestampStart, timestampEnd, peakValue));
                        }
                    }

                    timestampStart = 0;
                }
            }
        }

        return peaks.ToArray();
    }

    public void ReversePeakSearchDirection()
    {
        PeakThreshold = -PeakThreshold;
        Direction = Direction == PeakDirection.Up ? PeakDirection.Down : PeakDirection.Up;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PeakThreshold)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Direction)));
    }

    // Internal

    protected int _bufferSize = 12;

    protected (double, double) GetAverages(Sample[] samples)
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
        PeakDirection.Up => value < threshold,
        PeakDirection.Down => value > threshold,
        _ => throw new NotImplementedException($"{Direction} direction is not supported")
    };

    private bool IsAboveThreshold(double value, double threshold) => Direction switch
    {
        PeakDirection.Up => value > threshold,
        PeakDirection.Down => value < threshold,
        _ => throw new NotImplementedException($"{Direction} direction is not supported")
    };
}
