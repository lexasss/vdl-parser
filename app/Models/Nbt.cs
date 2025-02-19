using MathNet.Numerics.Statistics;
using System.IO;

namespace VdlParser.Models;

public record class NtbRecord(int Target, int? Response, bool IsCorrect, int? Delay, int TouchCount);

/// <summary>
/// N-Back task log data
/// </summary>
public class Nbt(string filename, int participantId, bool isNewCtt, bool isVr, double lambda, NtbRecord[] records) : IStatistics
{
    public string Filename => filename;
    public double Lambda => lambda;
    public int ParticipantID => participantId;
    public string Condition => isNewCtt ? (isVr ? "nctt+vr" : "nctt") : "octt";

    public static Nbt? Load(string filename)
    {
        var id = int.Parse(string.Join("", filename.Split(Path.DirectorySeparatorChar)[^3].Skip(1)) ?? "0");
        var newCttFilename = Utils.GetCorrespondingNewCtt(filename);
        var isVr = newCttFilename != null && CttNew.IsVR(newCttFilename);
        var lambda = newCttFilename != null ? Utils.GetLambda(newCttFilename) : 0;

        try
        {
            return new Nbt(Path.GetFileName(filename), id, newCttFilename != null, isVr, lambda, File
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

        (string, object)[] rows = [
            ("Filename", Filename),
            ("Participant ID", ParticipantID),
            ("Condition", Condition),
            ("Lambda", Lambda),
            ("Correctness", correctness),
            ("Response delay, mean", responseDelayMean),
            ("Response delay, SD", responseDelayStd),
        ];

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
