using System.ComponentModel;
using System.Text.Json;

namespace VdlParser;

public class GraphSettings : INotifyPropertyChanged
{
    public bool HasPupilSize
    {
        get => _hasPupilSize;
        set
        {
            _hasPupilSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPupilSize)));
        }
    }
    public bool HasPupilOpenness
    {
        get => _hasPupilOpenness;
        set
        {
            _hasPupilOpenness = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPupilOpenness)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static GraphSettings Load()
    {
        var settings = Properties.Settings.Default;
        string json = settings.Graph;

        var defaultSettings = new GraphSettings();

        if (string.IsNullOrEmpty(json))
        {
            return defaultSettings;
        }

        GraphSettings? result = null;
        try
        {
            result = JsonSerializer.Deserialize<GraphSettings>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }

        return result ?? defaultSettings;
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this);

        var settings = Properties.Settings.Default;
        settings.Graph = json;
        settings.Save();
    }

    // Internal

    bool _hasPupilSize = false;
    bool _hasPupilOpenness = false;
}
