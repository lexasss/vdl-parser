using System.IO;
using MathNet.Numerics.Statistics;

namespace VdlParser;

public record class CttOldRecord(double Interval, double Lambda, double LineOffset, double Input);

public class CttOld(string filename, int participantId, CttOldRecord[] records) : Statistics
{
    public string Filename => filename;
    public double Lambda => _records[0].Lambda;
    public int ParticipantID => participantId;
    public string Condition => "octt";

    public static CttOld? Load(string filename)
    {
        var id = int.Parse(string.Join("", filename.Split(Path.DirectorySeparatorChar)[^3].Skip(1)) ?? "0");

        try
        {
            double trainingDuration = 0;
            double testDuration = 0;

            return new CttOld(Path.GetFileName(filename), id, File
                .ReadAllLines(filename)
                .Skip(3)
                .Select(line =>
                {
                    var p = line.Split(", ");
                    return new CttOldRecord(double.Parse(p[0]), double.Parse(p[1]), double.Parse(p[2]), double.Parse(p[3]));
                })
                .SkipWhile(record => record.Lambda == 0)
                .SkipWhile(record =>
                {
                    trainingDuration += record.Interval;
                    return trainingDuration < TRAINING_DURATION;
                })
                .TakeWhile(record =>
                {
                    testDuration += record.Interval;
                    return testDuration < TEST_DURATION;
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

    public override string Get(StatisticsFormat format = StatisticsFormat.Rows)
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
            ("Offset, mean", offsetMean),
            ("Offset, SD", offsetSdt),
            ("Input, mean", inputMean),
            ("Input, SD", inputSdt),
        ];

        if (format == StatisticsFormat.List)
        {
            return string.Join('\n', rows.Select(row => $"{row.Item1} = {row.Item2}"));
        }

        return string.Join('\n', format == StatisticsFormat.RowHeaders ?
            rows.Skip(1).Select(row => row.Item1) :
            rows.Skip(1).Select(row => row.Item2));
    }

    // Internal

    const double TRAINING_DURATION = App.TRAINING_TRIAL_COUNT * App.TRIAL_DURATION;     // seconds
    const double TEST_DURATION = App.VALID_TRIAL_COUNT * App.TRAINING_TRIAL_COUNT;      // seconds

    readonly CttOldRecord[] _records = records;
}
