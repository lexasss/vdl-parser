using System.IO;

namespace VdlParser;

public class Vdl
{
    public string Name { get; }
    public int RecordCount { get; }

    public VdlRecord[] Records { get; }

    public Vdl(string name, VdlRecord[] records)
    {
        Name = name;
        RecordCount = records.Length;

        Records = records;
    }

    public static Vdl? Load(string filename)
    {
        long tsSystem = 0;
        long tsHeadset = 0;

        System.Diagnostics.Debug.WriteLine($"Loading: {Path.GetFileName(filename)}");

        var records = new List<VdlRecord>();
        using var reader = new StreamReader(filename);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var record = VdlRecord.Parse(line);

            if (record != null)
            {
                if (tsSystem == 0)
                {
                    tsSystem = record.TimestampSystem;
                    tsHeadset = record.TimestampHeadset;
                }

                var newRec = record with
                {
                    TimestampSystem = record.TimestampSystem - tsSystem,
                    TimestampHeadset = record.TimestampHeadset - tsHeadset,
                };
                records.Add(newRec);
            }
        }

        System.Diagnostics.Debug.WriteLine($"Record count: {records.Count}");

        var name = string.Join('-', Path.GetFileName(filename).Split('.')[0].Split('-')[1..]);
        return records.Count > 0 ? new Vdl(name, records.ToArray()) : null;
    }

    // Internal
}
