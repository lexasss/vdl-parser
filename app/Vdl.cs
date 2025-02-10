using System.IO;

namespace VdlParser;

public record class Rotation(double Pitch, double Yaw, double Roll);
public record class Vector3D(double X, double Y, double Z);
public record class Pupil(double Openness, double Size);

public enum NBackTaskEventType
{
    SessionStart,
    TrialStart,
    TrialResponse,
    TrialEnd,
    SessionEnd
}
public record class NBackTaskEvent(NBackTaskEventType Type);
public record class NBackTaskTrial(NBackTaskEventType Type, int Id) : NBackTaskEvent(Type);
public record class NBackTaskTrialResult(NBackTaskEventType Type, int Id, bool IsCorrect) : NBackTaskTrial(Type, Id);

public record class VdlRecord(
    long TimestampSystem,
    long TimestampHeadset,
    Rotation Eye, Rotation Head,
    Pupil LeftPupil, Pupil RightPupil,
    Vector3D HandPalm, Vector3D HandThumb, Vector3D HandIndex, Vector3D HandMiddle,
    NBackTaskEvent? NBackTaskEvent)
{
    public static VdlRecord? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var p = text.Split('\t');
        if (p.Length != 23)
            return null;

        VdlRecord? result = null;

        try
        {
            result = new VdlRecord(long.Parse(p[0]) / 10_000, long.Parse(p[1]) / 1_000_000,
                new Rotation(double.Parse(p[3]), double.Parse(p[2]), 0),
                new Rotation(double.Parse(p[5]), double.Parse(p[4]), 0),
                new Pupil(double.Parse(p[6]), double.Parse(p[7])),
                new Pupil(double.Parse(p[8]), double.Parse(p[9])),
                new Vector3D(double.Parse(p[10]), double.Parse(p[11]), double.Parse(p[12])),
                new Vector3D(double.Parse(p[13]), double.Parse(p[14]), double.Parse(p[15])),
                new Vector3D(double.Parse(p[16]), double.Parse(p[17]), double.Parse(p[18])),
                new Vector3D(double.Parse(p[19]), double.Parse(p[20]), double.Parse(p[21])),
                string.IsNullOrEmpty(p[22]) ? null :
                    p[22].Split(' ') switch
                    {
                    ["STR"] => new NBackTaskEvent(NBackTaskEventType.SessionStart),
                    ["SET", string id] => new NBackTaskTrial(NBackTaskEventType.TrialStart, int.Parse(id)),
                    ["ACT", string id] => new NBackTaskTrial(NBackTaskEventType.TrialResponse, int.Parse(id)),
                    ["RES", string id, string isSuccess] => new NBackTaskTrialResult(NBackTaskEventType.TrialEnd, int.Parse(id), bool.Parse(isSuccess)),
                    ["FIN"] => new NBackTaskEvent(NBackTaskEventType.SessionEnd),
                        _ => throw new Exception($"Unknown NBackTask event: {p[22]}")
                    }
            );
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Cannot parse the record: {text}");
        }

        return result;
    }
}

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
}
