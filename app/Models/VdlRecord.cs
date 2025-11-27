namespace VdlParser.Models;

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
    public double PupilOpenness => (LeftPupil.Openness + RightPupil.Openness) / 2;
    public double PupilSize => (LeftPupil.Size + RightPupil.Size) / 2;
    public Vector3D TopViewHandPalm { get; set; } = Vector3D.Zero;
    public Vector3D TopViewHandThumb { get; set; } = Vector3D.Zero;
    public Vector3D TopViewHandIndex { get; set; } = Vector3D.Zero;
    public Vector3D TopViewHandMiddle { get; set; } = Vector3D.Zero;

    public static object? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var p = text.Split('\t');

        if (p.Length == 1 && p[0].StartsWith("SET "))    // fixes the VDL+NBT recording bug
            return new NBackTaskTrial(NBackTaskEventType.TrialStart, int.Parse(text.Split(' ')[1]));
        if (p.Length < 23)
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
                string.IsNullOrEmpty(p[^1]) ? null :
                    p[^1].Split(' ') switch
                    {
                        ["STR"] => new NBackTaskEvent(NBackTaskEventType.SessionStart),
                        ["SET", string id] => new NBackTaskTrial(NBackTaskEventType.TrialStart, int.Parse(id)),
                        ["ACT", string id] => new NBackTaskTrial(NBackTaskEventType.TrialResponse, int.Parse(id)),
                        ["RES", string id, string isSuccess] => new NBackTaskTrialResult(NBackTaskEventType.TrialEnd, int.Parse(id), bool.Parse(isSuccess)),
                        ["FIN"] => new NBackTaskEvent(NBackTaskEventType.SessionEnd),
                        _ => throw new Exception($"Unknown NBackTask event: {p[^1]}")
                    }
            );

            if (p.Length == 35)
            {
                result.TopViewHandPalm = new Vector3D(double.Parse(p[22]), double.Parse(p[23]), double.Parse(p[24]));
                result.TopViewHandThumb = new Vector3D(double.Parse(p[25]), double.Parse(p[26]), double.Parse(p[27]));
                result.TopViewHandIndex = new Vector3D(double.Parse(p[28]), double.Parse(p[29]), double.Parse(p[30]));
                result.TopViewHandMiddle = new Vector3D(double.Parse(p[31]), double.Parse(p[32]), double.Parse(p[33]));
            }
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Cannot parse the record: {text}");
        }

        return result;
    }

    public static VdlRecord? FromVarjo(VarjoRecord r)
    {
        if (r.Status != VarjoTrackingStatus.Tracking) return null;

        double oneOverZ = 1.0 / r.GazeForward.Z;
        var yaw = RadiansToDegrees * Math.Atan(r.GazeForward.X * oneOverZ);
        var pitch = RadiansToDegrees * Math.Atan(r.GazeForward.Y * oneOverZ);

        return new VdlRecord(
            r.TimestampSystem, r.TimestampUnix,
            new Rotation(pitch, yaw, 0),
            Rotation.Zero,
            new Pupil(r.Left.EyeOpenness, r.Left.PupilDiameterInMm),
            new Pupil(r.Right.EyeOpenness, r.Right.PupilDiameterInMm),
            Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, Vector3D.Zero,
            null);
    }

    //  Internal
    
    const double RadiansToDegrees = 180.0 / Math.PI;
}
