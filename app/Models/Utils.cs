using System.IO;

namespace VdlParser.Models;

internal static class Utils
{
    public static DateTime ParseDateTime(string[] str)
    {
        int i = 0;
        while (!int.TryParse(str[i], out int _))
            i++;

        try
        {
            return new DateTime(
                int.Parse(str[i]), int.Parse(str[i + 1]), int.Parse(str[i + 2]),
                int.Parse(str[i + 3]), int.Parse(str[i + 4]), int.Parse(string.Join("", str[i + 5].Take(2))));
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

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
        var nbtTimestamp = ParseDateTime(timestampedFilename.Split(['-', ' ']).ToArray());

        var matchedNewCttFilename = ncttFiles.FirstOrDefault(ncttFilename =>
        {
            ncttFilename = Path.GetFileNameWithoutExtension(ncttFilename);
            var octtTimestamp = ParseDateTime(ncttFilename.Split(['-', ' ']).ToArray());
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
