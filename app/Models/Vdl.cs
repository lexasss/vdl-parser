using System.IO;

namespace VdlParser.Models;

public class Vdl
{
    public string Timestamp { get; }
    public string Participant { get; }
    public double Lambda { get; }
    public int RecordCount { get; }

    public VdlRecord[] Records { get; }

    public PupilCalibration? PupilCalibration { get; set; } = null;

    public Vdl(string timestamp, string participant, double lambda, VdlRecord[] records)
    {
        Timestamp = timestamp;
        Participant = participant;
        Lambda = lambda;
        RecordCount = records.Length;

        Records = records;
    }

    public static Vdl? Load(string filename)
    {
        long tsSystem = 0;
        long tsHeadset = 0;

        System.Diagnostics.Debug.WriteLine($"Loading: {Path.GetFileName(filename)}");

        var newCttFilename = Utils.GetCorrespondingNewCtt(filename);
        var lambda = newCttFilename != null ? Utils.GetLambda(newCttFilename) : 0;

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

        var participant = Path.GetDirectoryName(filename)?.Split(Path.DirectorySeparatorChar)[^3] ?? "";
        var timestamp = string.Join('-', Path.GetFileName(filename).Split('.')[0].Split('-')[1..]);
        return records.Count > 0 ? new Vdl(timestamp, participant, lambda, records.ToArray()) : null;
    }
}
