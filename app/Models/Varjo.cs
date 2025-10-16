using System.IO;

namespace VdlParser.Models;

public class Varjo
{
    public string Timestamp { get; }
    public int RecordCount { get; }

    public VarjoRecord[] Records { get; }

    public Varjo(string timestamp, VarjoRecord[] records)
    {
        Timestamp = timestamp;
        RecordCount = records.Length;
        Records = records;
    }

    public static Varjo? Load(string filename)
    {
        System.Diagnostics.Debug.WriteLine($"Loading: {Path.GetFileName(filename)}");

        var records = new List<VarjoRecord>();
        using var reader = new StreamReader(filename, new FileStreamOptions() {
            Access = FileAccess.Read, Mode = FileMode.Open, Share = FileShare.ReadWrite });

        reader.ReadLine();  // skip the first line (header)

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var record = VarjoRecord.Parse(line);

            if (record != null)
            {
                records.Add(record);
            }
        }

        System.Diagnostics.Debug.WriteLine($"Record count: {records.Count}");

        var timestamp = Path.GetFileName(filename).Split('.')[0].Split('_')[^1];
        return records.Count > 0 ? new Varjo(timestamp, records.ToArray()) : null;
    }
}
