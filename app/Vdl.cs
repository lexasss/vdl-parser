using System.IO;

namespace VdlParser;

public class Vdl
{
    public string Name { get; }
    public int RecordCount { get; }

    public Record[] Records { get; }

    public Vdl(string name, Record[] records)
    {
        Name = name;
        RecordCount = records.Length;

        Records = records;
    }

    public static Vdl? Load(string filename)
    {
        long t1 = 0;
        long t2 = 0;

        System.Diagnostics.Debug.WriteLine($"Loading: {Path.GetFileName(filename)}");

        var records = new List<Record>();
        using var reader = new StreamReader(filename);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var record = Record.Parse(line);

            if (record != null)
            {
                if (t1 == 0)
                {
                    t1 = record.TimestampSystem;
                    t2 = record.TimestampHeadset;
                }

                records.Add(record with
                {
                    TimestampSystem = record.TimestampSystem - t1,
                    TimestampHeadset = record.TimestampHeadset - t2
                });
            }
        }

        System.Diagnostics.Debug.WriteLine($"Record count: {records.Count}");

        var name = string.Join('-', Path.GetFileName(filename).Split('.')[0].Split('-')[1..]);
        return records.Count > 0 ? new Vdl(name, records.ToArray()) : null;
    }

    // Internal
}
