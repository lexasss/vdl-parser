using MathNet.Numerics.Statistics;
using System.IO;

namespace VdlParser.Models;

public class PupilCalibration(double size)
{
    public double Size { get; } = size;

    public static PupilCalibration? Load(string filename)
    {
        System.Diagnostics.Debug.WriteLine($"Loading: {Path.GetFileName(filename)}");

        var pupilSize = File.ReadAllLines(filename)
            .Skip(100)      // just skip the very first second or two
            .Select(line => VdlRecord.Parse(line)!)
            .Where(record => record.LeftPupil.Openness > 0.7 && record.RightPupil.Openness > 0.7)
            .Select(record => (record.LeftPupil.Size + record.RightPupil.Size) / 2)
            .Mean();

        return new PupilCalibration(pupilSize);
    }
}
