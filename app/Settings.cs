using System.ComponentModel;

namespace VdlParser;

public enum TimestampSource
{
    System,
    Headset
}

public class Settings : INotifyPropertyChanged
{
    public static Settings Instance => _instance ??= new();

    // Inter-session

    public HandDataSource HandDataSource { get; set; } = HandDataSource.IndexFinger;
    public GazeDataSource GazeDataSource { get; set; } = GazeDataSource.YawRotation;
    public TimestampSource TimestampSource { get; set; } = TimestampSource.System;
    public int MaxHandGazeDelay { get; set; } = 1500; // ms
    public double QuantileThreshold { get; set; } = 0.1;

    public string LogFolder
    {
        get => _logFolder;
        set
        {
            _logFolder = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogFolder)));
        }
    }

    // Session-only

    // Events

    public event EventHandler? Updated;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Save()
    {
        var settings = Properties.Settings.Default;

        settings.HandDataSource = (int)HandDataSource;
        settings.GazeDataSource = (int)GazeDataSource;
        settings.MaxHandGazeDelay = MaxHandGazeDelay;
        settings.TimestampSource = (int)TimestampSource;
        settings.QuantileThreshold = QuantileThreshold;

        settings.LogFolder = LogFolder;

        settings.Save();
    }

    // Internal

    static Settings? _instance = null;

    string _logFolder = "";

    private Settings()
    {
        Load();

        App.Current.Exit += (s, e) => Save();
    }

    private void Load()
    {
        var settings = Properties.Settings.Default;

        HandDataSource = (HandDataSource)settings.HandDataSource;
        GazeDataSource = (GazeDataSource)settings.GazeDataSource;
        MaxHandGazeDelay  = settings.MaxHandGazeDelay;
        TimestampSource = (TimestampSource)settings.TimestampSource;
        QuantileThreshold = settings.QuantileThreshold;

        LogFolder = settings.LogFolder;
    }
}
