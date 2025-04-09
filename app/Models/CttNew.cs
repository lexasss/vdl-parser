using MathNet.Numerics.Statistics;
using System.IO;

namespace VdlParser.Models;

public record class CttNewRecord(long Timestamp, double Lambda, double LineOffset, double Input);

public class CttNew(string filename, int participantId, bool isVr, CttNewRecord[] records) : IStatistics
{
    public string Filename => filename;
    public double Lambda => _records[0].Lambda;
    public int ParticipantID => participantId;
    public string Condition => isVr ? "nctt+vr" : "nctt";

    public static CttNew? Load(string filename)
    {
        var id = int.Parse(string.Join("", filename.Split(Path.DirectorySeparatorChar)[^3].Skip(1)) ?? "0");
        var isVr = IsVR(filename);

        try
        {
            long startTimestamp = 0;

            return new CttNew(Path.GetFileName(filename), id, isVr, File
                .ReadAllLines(filename)
                .Skip(1)
                .Select(line =>
                {
                    var p = line.Split('\t');
                    long timestamp = long.Parse(p[0]);
                    if (startTimestamp == 0)
                    {
                        startTimestamp = timestamp;
                        timestamp = 0;
                    }
                    else
                    {
                        timestamp -= startTimestamp;
                    }
                    return new CttNewRecord(timestamp / 10000, double.Parse(p[1]), double.Parse(p[2]), double.Parse(p[3]));
                })
                .SkipWhile(record => record.Timestamp < TRAINING_DURATION)
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
        var (offsetMean, offsetSdt) = _records
            .Select(record => Math.Abs(record.LineOffset))
            .MeanStandardDeviation();
        var (inputMean, inputSdt) = _records
            .Select(record => Math.Abs(record.Input))
            .MeanStandardDeviation();

        (string, object)[] rows = [
            ("Filename", Filename),
            ("Participant ID", ParticipantID),
            ("Condition", Condition),
            ("Lambda", Lambda),
            ("Offset, mean", 100 * offsetMean),
            ("Offset, SD", 100 * offsetSdt),
            ("Input, mean", 100 * inputMean),
            ("Input, SD", 100 * inputSdt),
        ];

        if(format == Format.List)
        {
            return string.Join('\n', rows.Select(row => $"{row.Item1} = {row.Item2}"));
        }

        return string.Join('\n', format == Format.RowHeaders ?
            rows.Skip(1).Select(row => row.Item1) :
            rows.Skip(1).Select(row => row.Item2));
    }

    public static bool IsVR(string cttFilename)
    {
        var folder = Path.GetDirectoryName(cttFilename) ?? "";
        var vdlFolder = Path.Combine(folder, "VDL");
        var vdlFiles = Directory.GetFiles(vdlFolder);

        cttFilename = Path.GetFileNameWithoutExtension(cttFilename);
        var cttTimestamp = Utils.ParseDateTime(cttFilename.Split(['-', ' ']).ToArray());

        var matchedVdlFilename = vdlFiles.FirstOrDefault(vdlFilename =>
        {
            vdlFilename = Path.GetFileNameWithoutExtension(vdlFilename);
            var vdlTimestamp = Utils.ParseDateTime(vdlFilename.Split(['-', ' ']).ToArray());
            var interval = vdlTimestamp - cttTimestamp;
            return Math.Abs(interval.TotalSeconds) < 30;
        });

        return matchedVdlFilename != null;
    }

    // Internal

    const long TRAINING_DURATION = App.TRAINING_TRIAL_COUNT * App.TRIAL_DURATION * 1000;     // ms

    readonly CttNewRecord[] _records = records;
}
