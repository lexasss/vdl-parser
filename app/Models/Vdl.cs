using System.IO;

namespace VdlParser.Models;

public class Vdl
{
    public string Timestamp { get; }
    public int RecordCount { get; }
    public TestCondition Condition { get; }

    public VdlRecord[] Records { get; }

    public PupilCalibration? PupilCalibration { get; set; } = null;

    public Vdl(string timestamp, VdlRecord[] records, TestCondition? condition = null)
    {
        Timestamp = timestamp;
        RecordCount = records.Length;

        Records = records;
        Condition = condition ?? new TestCondition();
    }

    public static Vdl? Load(string filename, TestCondition? testCondition = null)
    {
        long tsSystem = 0;
        long tsHeadset = 0;

        NBackTaskEvent? nbtEvent = null;

        System.Diagnostics.Debug.WriteLine($"Loading: {Path.GetFileName(filename)}");

        var records = new List<VdlRecord>();
        using var reader = new StreamReader(filename);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var obj = VdlRecord.Parse(line);

            if (obj is VdlRecord record)
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

                if (nbtEvent != null && newRec.NBackTaskEvent == null)   // fixes issue where NBackTaskEvent appears on a separate line
                {
                    newRec = newRec with { NBackTaskEvent = nbtEvent };
                    nbtEvent = null;
                }

                records.Add(newRec);
            }
            else if (obj is NBackTaskEvent trialStartEvent)
            {
                nbtEvent = trialStartEvent;
            }
        }

        System.Diagnostics.Debug.WriteLine($"Record count: {records.Count}");

        var timestamp = string.Join('-', Path.GetFileName(filename).Split('.')[0].Split('-')[1..]);

        return records.Count > 0 ? new Vdl(timestamp, records.ToArray(), testCondition) : null;
    }

    public static Vdl? FromVarjo(Varjo varjo)
    {
        long tsSystem = 0;
        long tsHeadset = 0;

        System.Diagnostics.Debug.WriteLine($"Loading from Varjo...");

        var records = new List<VdlRecord>();

        foreach (var r in varjo.Records)
        {
            var record = VdlRecord.FromVarjo(r);

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

        return records.Count > 0 ? new Vdl(varjo.Timestamp, records.ToArray()) : null;
    }
}
