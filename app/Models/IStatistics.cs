namespace VdlParser.Models;

public enum Format
{
    List,
    Rows,
    RowHeaders
}

public interface IStatistics
{
    string Get(Format format);
}
