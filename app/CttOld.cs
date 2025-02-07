using System.IO;
using MathNet.Numerics.Statistics;

namespace VdlParser;

public record class CttOldRecord(double Interval, double Lambda, double LineOffset, double Input);

public class CttOld(int participantId, CttOldRecord[] records)
{
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

            return new CttOld(id, File
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

    public override string ToString()
    {
        var (offsetMean, offsetSdt) = _records
            .Select(record => Math.Abs(record.LineOffset))
            .MeanStandardDeviation();
        var (inputMean, inputSdt) = _records
            .Select(record => Math.Abs(record.Input))
            .MeanStandardDeviation();
        return string.Join('\n',
            offsetMean,
            offsetSdt,
            inputMean,
            inputSdt
        );
    }

    // Internal

    const double TRAINING_DURATION = App.TRAINING_TRIAL_COUNT * App.TRIAL_DURATION;     // seconds
    const double TEST_DURATION = App.VALID_TRIAL_COUNT * App.TRAINING_TRIAL_COUNT;      // seconds

    readonly CttOldRecord[] _records = records;
}
