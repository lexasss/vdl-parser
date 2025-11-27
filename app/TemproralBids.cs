using MathNet.Numerics.Statistics;

namespace VdlParser;

public record class Timestamped(long Timestamp, double Value);

public record class Bid(double Mean, int Size);

public class TemproralBids
{
    public int BidCount { get; set; } = 5;

    public Bid[] Get(Timestamped[] points, long startTimestamp, long endTimestamp)
    {
        var bidSize = (double)(endTimestamp - startTimestamp) / BidCount;
        var bids = new List<double>[BidCount];
        foreach (var i in Enumerable.Range(0, BidCount))
        {
            bids[i] = [];
        }

        int bidID = 0;
        double bidEdge = startTimestamp + bidSize;

        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            while ((point.Timestamp - bidEdge) > EPSILON && bidID < BidCount - 1)
            {
                bidID += 1;
                bidEdge += bidSize;
            }
            bids[bidID].Add(point.Value);
        }

        return bids
            .Select(bid => new Bid(bid?.Median() ?? 0, bid?.Count ?? 0))
            .ToArray();
    }

    // Internal

    const double EPSILON = 0.1;
}
