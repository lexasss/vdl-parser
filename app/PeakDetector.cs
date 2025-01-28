﻿using MathNet.Numerics.Statistics;

namespace VdlParser;

public record class Sample(long Timestamp, double Value);
public record class Peak(int StartIndex, long TimestampStart, long TimestampEnd, double Amplitude);

public class PeakDetector
{
    public int BufferSize
    {
        get => _bufferSize;
        set => _bufferSize = Math.Max(3, value);
    }

    public double PeakThreshold { get; set; } = 1.5;
    public double IgnoranceThrehold { get; set; } = 20;
    public long MaxPeakDuration { get; set; } = 2500;

    public Peak[] Find(Sample[] timeseries)
    {
        var peaks = new List<Peak>();

        int i = BufferSize;

        bool isInPeak = false;
        long timestampStart = 0;
        int timestampStartIndex = 0;

        bool SeekBufferHead()
        {
            var firstValidDatapointIndex = i;
            while (firstValidDatapointIndex < timeseries.Length && timeseries[firstValidDatapointIndex].Value < IgnoranceThrehold)
            {
                firstValidDatapointIndex += 1;
            }

            i = firstValidDatapointIndex;
            for (int j = i + 1; (i + j) < timeseries.Length && j < BufferSize; j++)
            {
                if (timeseries[++i].Value < IgnoranceThrehold)
                {
                    return SeekBufferHead();
                }
            }

            return i < timeseries.Length;
        }

        if (!SeekBufferHead())
            return peaks.ToArray();

        while (++i < timeseries.Length)
        {
            if (timeseries[i].Value < IgnoranceThrehold && !SeekBufferHead())
                break;

            var chunk = timeseries[(i - BufferSize)..i];
            var (avg1, avg2) = GetAverages(chunk);
            var difference = avg2 - avg1;

            if (!isInPeak && difference > PeakThreshold)
            {
                isInPeak = true;

                timestampStart = chunk[_bufferSize / 2].Timestamp;
                timestampStartIndex = i - _bufferSize / 2;
            }
            else if (isInPeak && difference < -PeakThreshold)
            {
                isInPeak = false;

                if (timestampStart != 0)
                {
                    var timestampEnd = chunk[_bufferSize / 2].Timestamp;

                    if ((timestampEnd - timestampStart) < MaxPeakDuration)
                    {
                        var peakValue = timeseries[timestampStartIndex..i].Select(s => s.Value).Median();
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
}
