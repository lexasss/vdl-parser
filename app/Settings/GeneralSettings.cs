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
    public int MaxTrialStartToGazePeakStartInterval { get; set; } = 250; // ms, the glance can start up to this interval before the trial starts (the audio instruction is over)
    public int MaxGazePeakEndToNextTrialStartInterval { get; set; } = 250; // ms, the glance can end this much delayed inside the next trial
    public int MaxGazePeakStartToHandPeakStartInterval { get; set; } = 750; // ms, the glance can start no later than 750 after the hand peak started
    public int MinResponseToGazePeakStartInterval { get; set; } = 250; // ms, the glance cannot start very close or after to the time the response was given
    public double MaxInvalidHandDataShare { get; set; } = 0.8;      // share of invalid data in hand data during a trial above which the trial is considered invalid
    public double GazeMovementFactor { get; set; } = 0.5;      // % from the gaze data STD: trials with lower than this value time STD are considered as no-gaze-movement trials
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
