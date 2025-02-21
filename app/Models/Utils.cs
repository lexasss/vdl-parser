using System.IO;

namespace VdlParser.Models;

internal static class Utils
{
    public static DateTime ParseDateTime(string[] str) =>
        new DateTime(
            int.Parse(str[0]), int.Parse(str[1]), int.Parse(str[2]),
            int.Parse(str[3]), int.Parse(str[4]), int.Parse(string.Join("", str[5].SkipLast(1))));

    public static string? GetCorrespondingNewCtt(string timestampedFilename)
    {
        var folder = Path.GetDirectoryName(timestampedFilename) ?? "";
        while (!Directory.Exists(Path.Combine(folder, "CTT")))
        {
            folder = Path.GetDirectoryName(folder) ?? "";
            if (string.IsNullOrEmpty(folder))
                return null;
        }

        var cttFolder = Path.Combine(folder, "CTT");
        var ncttFiles = Directory.GetFiles(cttFolder, "ctt-*.txt");

        timestampedFilename = Path.GetFileNameWithoutExtension(timestampedFilename);
        var nbtTimestamp = ParseDateTime(timestampedFilename.Split(['-', ' ']).TakeLast(6).ToArray());

        var matchedNewCttFilename = ncttFiles.FirstOrDefault(ncttFilename =>
        {
            ncttFilename = Path.GetFileNameWithoutExtension(ncttFilename);
            var octtTimestamp = ParseDateTime(ncttFilename.Split(['-', ' ']).TakeLast(6).ToArray());
            var interval = nbtTimestamp - octtTimestamp;
            return Math.Abs(interval.TotalSeconds) < 30;
        });

        return matchedNewCttFilename;
    }

    public static double GetLambda(string ncttFilename)
    {
        using var reader = new StreamReader(ncttFilename);

        reader.ReadLine();
        var line = reader.ReadLine() ?? "";
        var lambda = double.Parse(line.Split('\t')[1]);
        return lambda;
    }
}
