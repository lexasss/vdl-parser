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
public class NBackTaskEvent(long timestamp, NBackTaskEventType type)
{
    public long Timestamp { get; set; } = timestamp;
    public NBackTaskEventType Type => type;
}
public class NBackTaskTrial(long timestamp, NBackTaskEventType type, int id) : NBackTaskEvent(timestamp, type)
{
    public int Id => id;
}
public class NBackTaskTrialResult(long timestamp, NBackTaskEventType type, int id, bool isSuccess) : NBackTaskTrial(timestamp, type, id)
{
    public bool IsSuccess => isSuccess;
}

public record class Record(
    long TimestampSystem,
    long TimestampHeadset,
    Rotation Eye, Rotation Head,
    Pupil LeftPupil, Pupil RightPupil,
    Vector3D HandPalm, Vector3D HandThumb, Vector3D HandIndex, Vector3D HandMiddle,
    NBackTaskEvent? NBackTaskEvent)
{
    public static Record? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var p = text.Split('\t');
        if (p.Length != 23)
            return null;

        Record? result = null;

        try
        {
            long ts = long.Parse(p[0]) / 10000;
            result = new Record(ts, long.Parse(p[1]) / 10000,
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
                        ["STR"] => new NBackTaskEvent(ts, NBackTaskEventType.SessionStart),
                        ["SET", string id] => new NBackTaskTrial(ts, NBackTaskEventType.TrialStart, int.Parse(id)),
                        ["ACT", string id] => new NBackTaskTrial(ts, NBackTaskEventType.TrialResponse, int.Parse(id)),
                        ["RES", string id, string isSuccess] => new NBackTaskTrialResult(ts, NBackTaskEventType.TrialEnd, int.Parse(id), bool.Parse(isSuccess)),
                        ["FIN"] => new NBackTaskEvent(ts, NBackTaskEventType.SessionEnd),
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
