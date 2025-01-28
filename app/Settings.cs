using System.ComponentModel;

namespace VdlParser;

internal class Settings : INotifyPropertyChanged
{
    public static Settings Instance => _instance ??= new();

    // Inter-session

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

    public void ShowDialog()
    {
        /*
        var modifiedSettings = new Settings();
        var dialog = new SettingsDialog(modifiedSettings);
        if (dialog.ShowDialog() ?? false)
        {
            modifiedSettings.Save();

            Load();
            Updated?.Invoke(this, EventArgs.Empty);
        }*/
    }

    public void Save()
    {
        var settings = Properties.Settings.Default;

        settings.LogFolder = LogFolder;

        settings.Save();
    }

    // Internal

    static Settings? _instance = null;

    string _logFolder = "";

    private Settings()
    {
        Load();
    }

    private void Load()
    {
        var settings = Properties.Settings.Default;

        LogFolder = settings.LogFolder;
    }
}
