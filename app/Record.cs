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
public record class NBackTaskTrialResult(NBackTaskEventType Type, int Id, bool IsSuccess) : NBackTaskTrial(Type, Id);

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
            result = new Record(long.Parse(p[0]) / 10_000, long.Parse(p[1]) / 1_000_000,
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
                        ["STR"] => new NBackTaskEvent( NBackTaskEventType.SessionStart),
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
