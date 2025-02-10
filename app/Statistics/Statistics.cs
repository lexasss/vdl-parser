namespace VdlParser.Statistics;

public enum Format
{
    List,
    Rows,
    RowHeaders
}

public abstract class Statistics
{
    public static double QuantileThreshold { get; set; } = 0.1;

    public abstract string Get(Format format);
}
