using MathNet.Numerics.Statistics;
using System.IO;

namespace VdlParser;

public record class NtbRecord(int target, int? Response, bool IsCorrect, int? Delay, int TouchCount);

/// <summary>
/// N-Back task log data
/// </summary>
public class Nbt(int participantId, bool isNewCtt, bool isVr, double lambda, NtbRecord[] records)
{
    public double Lambda => lambda;
    public int ParticipantID => participantId;
    public string Condition => isNewCtt ? (isVr ? "nctt+vr" : "nctt") : "octt";

    public static Nbt? Load(string filename)
    {
        var id = int.Parse(string.Join("", filename.Split(Path.DirectorySeparatorChar)[^3].Skip(1)) ?? "0");
        var newCttFilename = GetCorrespondingNewCtt(filename);
        var isVr = newCttFilename != null ? CttNew.IsVR(newCttFilename) : false;
        var lambda = newCttFilename != null ? GetLambda(newCttFilename) : 0;

        try
        {
            return new Nbt(id, newCttFilename != null, isVr, lambda, File
                .ReadAllLines(filename)
                .SkipWhile(line => !line.StartsWith("#"))
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

    public override string ToString()
    {
        var correctness = 1.0 * _records.Sum(_records => _records.IsCorrect ? 1 : 0) / _records.Length;
        var (responseDelayMean, responseDelayStd) = _records
            .Where(record => record.Delay != null)
            .Select(record => (double)(record.Delay ?? 0))
            .MeanStandardDeviation();
        return string.Join('\n',
            correctness,
            responseDelayMean,
            responseDelayStd
        );
    }

    // Internal

    readonly NtbRecord[] _records = records;

    private static string? GetCorrespondingNewCtt(string nbtFilename)
    {
        var folder = Path.GetDirectoryName(nbtFilename) ?? "";
        var cttFolder = string.Join(Path.DirectorySeparatorChar, folder.Split(Path.DirectorySeparatorChar).SkipLast(1).Append("CTT"));
        var ncttFiles = Directory.GetFiles(cttFolder, "ctt-*.txt");

        nbtFilename = Path.GetFileNameWithoutExtension(nbtFilename);
        var nbtTimestamp = App.ParseDateTime(nbtFilename.Split(['-', ' ']).Skip(3).ToArray());

        var matchedNewCttFilename = ncttFiles.FirstOrDefault(ncttFilename =>
        {
            ncttFilename = Path.GetFileNameWithoutExtension(ncttFilename);
            var octtTimestamp = App.ParseDateTime(ncttFilename.Split(['-', ' ']).Skip(1).ToArray());
            var interval = nbtTimestamp - octtTimestamp;
            return Math.Abs(interval.TotalSeconds) < 30;
        });

        return matchedNewCttFilename;
    }

    private static double GetLambda(string ncttFilename)
    {
        using var reader = new StreamReader(ncttFilename);

        reader.ReadLine();
        var line = reader.ReadLine() ?? "";
        var lambda = double.Parse(line.Split('\t')[1]);
        return lambda;
    }
}
