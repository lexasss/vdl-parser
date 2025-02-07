using MathNet.Numerics.Statistics;

namespace VdlParser;

public record class Timestamped(long Timestamp, double Value);

public class TemproralBids
{
    public int BidCount { get; set; } = 5;

    public double[] Get(Timestamped[] points)
    {
        if (points.Length < BidCount)
            return [];

        var bidSize = (double)(points[^1].Timestamp - points[0].Timestamp) / BidCount;
        var bids = new List<double>[BidCount];

        int bidID = 0;
        bids[0] = new List<double>();
        double bidEdge = points[0].Timestamp + bidSize;

        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            while (point.Timestamp > bidEdge)
            {
                bidID += 1;
                bids[bidID] = new List<double>();
                bidEdge += bidSize;
            }
            bids[bidID].Add(point.Value);
        }

        return bids
            .Select(bid => bid?.Mean() ?? 0)
            .ToArray();
    }
}
