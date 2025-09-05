namespace VdlParser.Models;

public enum VarjoTrackingStatus
{
    NoEyesDetected = 0,
    EyesDetected = 1,
    Tracking = 2,
    Calibrating = 3,
    Calibrated = 4
}

public record class VarjoEye(
    Vector3D Forward,
    Vector3D Origin,
    double PupilSize,
    VarjoTrackingStatus Status,
    Vector2D Projected,
    double IrisDiameterInMm,
    double PupilDiameterInMm,
    double PupilIrisDiameterRatio,
    double EyeOpenness
);

public record class VarjoRecord(
    long TimestampSystem,
    long TimestampUnix,
    long TimestampVideo,

    double FocusDistance,
    int FrameNumber,
    double Stability,
    int Status,

    Vector3D GazeForward,
    Vector3D GazeOrigin,

    Vector2D GazeProjectedToLeftView,
    Vector2D GazeProjectedToRightView,

    VarjoEye Left,
    VarjoEye Right,

    double InterPupillaryDistanceInMm)
{

    public static VarjoRecord? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var p = text.Split(',');
        if (p.Length != 46)
            return null;

        VarjoRecord? result = null;

        try
        {
            result = new VarjoRecord(long.Parse(p[0]) / 1_000_000, long.Parse(p[1]) / 1_000_000, long.Parse(p[2]) / 1_000_000,
                double.Parse(p[3]), int.Parse(p[4]),
                double.Parse(p[5]), int.Parse(p[6]),
                new Vector3D(double.Parse(p[7]), double.Parse(p[8]), double.Parse(p[9])),
                new Vector3D(double.Parse(p[10]), double.Parse(p[11]), double.Parse(p[12])),
                Vector2D.Parse(p[13], p[14]),
                Vector2D.Parse(p[15], p[16]),
                new VarjoEye(
                    new Vector3D(double.Parse(p[17]), double.Parse(p[18]), double.Parse(p[19])),
                    new Vector3D(double.Parse(p[20]), double.Parse(p[21]), double.Parse(p[22])),
                    double.Parse(p[23]), (VarjoTrackingStatus)int.Parse(p[24]),
                    Vector2D.Parse(p[25], p[26]),
                    double.Parse(p[38]), double.Parse(p[39]), double.Parse(p[40]), double.Parse(p[41])
                ),
                new VarjoEye(
                    new Vector3D(double.Parse(p[27]), double.Parse(p[28]), double.Parse(p[29])),
                    new Vector3D(double.Parse(p[30]), double.Parse(p[31]), double.Parse(p[32])),
                    double.Parse(p[33]), (VarjoTrackingStatus)int.Parse(p[34]),
                    Vector2D.Parse(p[35], p[36]),
                    double.Parse(p[42]), double.Parse(p[43]), double.Parse(p[44]), double.Parse(p[45])
                ),
                double.Parse(p[37])
            );
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Cannot parse the record: {text}");
        }

        return result;
    }
}