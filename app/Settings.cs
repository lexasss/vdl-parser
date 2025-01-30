using System.ComponentModel;

namespace VdlParser;

public class Settings : INotifyPropertyChanged
{
    public static Settings Instance => _instance ??= new();

    // Inter-session

    public Finger Finger { get; set; } = Finger.Index;
    public GazeRotation GazeRotation { get; set; } = GazeRotation.Yaw;
    public int MaxFingerGazeDelay { get; set; } = 1500; // ms

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

        settings.Finger = (int)Finger;
        settings.GazeRotation = (int)GazeRotation;
        settings.MaxFingerGazeDelay = MaxFingerGazeDelay;

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

        Finger = (Finger)settings.Finger;
        GazeRotation = (GazeRotation)settings.GazeRotation;
        MaxFingerGazeDelay  = settings.MaxFingerGazeDelay;

        LogFolder = settings.LogFolder;
    }
}
