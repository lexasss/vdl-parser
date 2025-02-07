using MathNet.Numerics.Statistics;
using System.IO;

namespace VdlParser;

public record class CttNewRecord(long Timestamp, double Lambda, double LineOffset, double Input);

public class CttNew(int participantId, bool isVr, CttNewRecord[] records)
{
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

            return new CttNew(id, isVr, File
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

    public override string ToString()
    {
        var (offsetMean, offsetSdt) = _records
            .Select(record => Math.Abs(record.LineOffset))
            .MeanStandardDeviation();
        var (inputMean, inputSdt) = _records
            .Select(record => Math.Abs(record.Input))
            .MeanStandardDeviation();
        return string.Join('\n',
            100 * offsetMean,
            100 * offsetSdt,
            100 * inputMean,
            100 * inputSdt
        );
    }

    public static bool IsVR(string cttFilename)
    {
        var folder = Path.GetDirectoryName(cttFilename) ?? "";
        var vdlFolder = Path.Combine(folder, "VDL");
        var vdlFiles = Directory.GetFiles(vdlFolder);

        cttFilename = Path.GetFileNameWithoutExtension(cttFilename);
        var cttTimestamp = App.ParseDateTime(cttFilename.Split(['-', ' ']).Skip(1).ToArray());

        var matchedVdlFilename = vdlFiles.FirstOrDefault(vdlFilename =>
        {
            vdlFilename = Path.GetFileNameWithoutExtension(vdlFilename);
            var vdlTimestamp = App.ParseDateTime(vdlFilename.Split(['-', ' ']).Skip(1).Take(6).ToArray());
            var interval = vdlTimestamp - cttTimestamp;
            return Math.Abs(interval.TotalSeconds) < 30;
        });

        return matchedVdlFilename != null;
    }

    // Internal

    const long TRAINING_DURATION = App.TRAINING_TRIAL_COUNT * App.TRIAL_DURATION * 1000;     // ms

    readonly CttNewRecord[] _records = records;
}
