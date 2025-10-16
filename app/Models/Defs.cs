namespace VdlParser.Models;

public record class Rotation(double Pitch, double Yaw, double Roll)
{
    public static readonly Rotation Zero = new(0, 0, 0);
}

public record class Vector2D(double X, double Y)
{
    public static readonly Vector2D Zero = new(0, 0);

    // Supports Varjo logfile NaN/Inf values
    public static Vector2D Parse(string x, string y)
    {
        if (x.Contains("nan") || y.Contains("nan"))
            return Zero;
        if (x.Contains("inf") || y.Contains("inf"))
            return Zero;
        return new Vector2D(double.Parse(x), double.Parse(y));
    }
}

public record class Vector3D(double X, double Y, double Z)
{
    public static readonly Vector3D Zero = new(0, 0, 0);
}

