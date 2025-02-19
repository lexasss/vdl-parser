namespace VdlParser;

public enum TimestampSource
{
    System,
    Headset
}

public class GeneralSettings : ISettings
{
    public string Section => "General";

    public static GeneralSettings Instance => _instance ??= Storage.Load<GeneralSettings>();


    public HandDataSource HandDataSource { get; set; } = HandDataSource.IndexFinger;
    public GazeDataSource GazeDataSource { get; set; } = GazeDataSource.PitchRotation;
    public TimestampSource TimestampSource { get; set; } = TimestampSource.System;
    public int MaxHandGazeDelay { get; set; } = 1500; // ms
    public double QuantileThreshold { get; set; } = 0.1;

    /// <summary>
    /// Do not use with the *new* operator, use <see cref="Instance"/> instead
    /// </summary>
    public GeneralSettings()
    {
        App.Current.Exit += (s, e) => Storage.Save(this);
    }

    // Internal

    static GeneralSettings? _instance = null;
}
