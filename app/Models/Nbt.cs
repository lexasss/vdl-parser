using MathNet.Numerics.Statistics;
using System.IO;

namespace VdlParser.Models;

public record class NtbRecord(int Target, int? Response, bool IsCorrect, int? Delay, int TouchCount);

/// <summary>
/// N-Back task log data
/// </summary>
public class Nbt(string filename, (string, object)[] conditions, NtbRecord[] records) : IStatistics
{
    public string Filename => filename;
    public (string, object)[] Conditions => conditions;

    public static Nbt? Load(string filename, string? cttFilename = null, TestCondition? condition = null)
    {
        var isHeadGazeStudy = cttFilename != null;

        var id = int.Parse(string.Join("", filename.Split(Path.DirectorySeparatorChar)[^3].Skip(1)) ?? "0");
        var newCttFilename = cttFilename ?? Utils.GetCorrespondingNewCtt(filename);
        var isVr = newCttFilename != null && (isHeadGazeStudy || CttNew.IsVR(newCttFilename));
        var lambda = newCttFilename != null ? Utils.GetLambda(newCttFilename) : 0;

        try
        {
            var conditions = condition?.AsArray() ?? [
                ("Participant", id),
                ("Condition", newCttFilename != null ? (isVr ? "nctt+vr" : "nctt") : "octt"),
                ("Lambda", lambda)
];

            var conditionName = newCttFilename != null ? (isVr ? "nctt+vr" : "nctt") : "octt";
            return new Nbt(Path.GetFileName(filename), conditions, File
                .ReadAllLines(filename)
                .SkipWhile(line => !line.StartsWith('#'))
                .Skip(2)
                .Skip(App.TRAINING_TRIAL_COUNT)
                .Select(line =>
                {
                    var p = line.Split('\t');
                    return new NtbRecord(int.Parse(p[0]),
                        string.IsNullOrEmpty(p[1]) ? null : int.Parse(p[1]),
                        p[2] == "OK",
                        string.IsNullOrEmpty(p[3]) ? null : int.Parse(p[3]),
                        int.Parse(p[4]));
                })
                .ToArray()
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{filename}:\n  {ex}");
        }

        return null;
    }

    public string Get(Format format = Format.Rows)
    {
        var correctness = 1.0 * _records.Sum(_records => _records.IsCorrect ? 1 : 0) / _records.Length;
        var (responseDelayMean, responseDelayStd) = _records
            .Where(record => record.Delay != null)
            .Select(record => (double)(record.Delay ?? 0))
            .MeanStandardDeviation();

        List<(string, object)> rows = [];
        rows.Add(("Filename", Filename));
        foreach (var condition in Conditions)
        {
            rows.Add(condition);
        }
        rows.Add(("Correctness", correctness));
        rows.Add(("Response delay, mean", responseDelayMean));
        rows.Add(("Response delay, SD", responseDelayStd));

        if (format == Format.List)
        {
            return string.Join('\n', rows.Select(row => $"{row.Item1} = {row.Item2}"));
        }

        return string.Join('\n', format == Format.RowHeaders ?
            rows.Skip(1).Select(row => row.Item1) :
            rows.Skip(1).Select(row => row.Item2));
    }

    // Internal

    readonly NtbRecord[] _records = records;
}
