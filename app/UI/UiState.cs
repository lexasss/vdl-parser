using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace VdlParser;

public class UiState : INotifyPropertyChanged
{
    public bool AreHandPeakDetectorSettingsVisible
    { 
        get => _areHandPeakDetectorSettingsVisible;
        set
        {
            _areHandPeakDetectorSettingsVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AreHandPeakDetectorSettingsVisible)));
        }
    }
    public bool AreGazePeakDetectorSettingsVisible
    {
        get => _areGazePeakDetectorSettingsVisible;
        set
        {
            _areGazePeakDetectorSettingsVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AreGazePeakDetectorSettingsVisible)));
        }
    }
    public bool AreBlinkDetectorSettingsVisible
    {
        get => _areBlinkDetectorSettingsVisible;
        set
        {
            _areBlinkDetectorSettingsVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AreBlinkDetectorSettingsVisible)));
        }
    }
    public bool AreBlinkDetector2SettingsVisible
    {
        get => _areBlinkDetector2SettingsVisible;
        set
        {
            _areBlinkDetector2SettingsVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AreBlinkDetector2SettingsVisible)));
        }
    }
    public bool AreOtherSettingsVisible
    {
        get => _areOtherSettingsVisible;
        set
        {
            _areOtherSettingsVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AreOtherSettingsVisible)));
        }
    }

    public bool IsSettingsPanelVisible
    {
        get => _isSettingsPanelVisible;
        set
        {
            _isSettingsPanelVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSettingsPanelVisible)));
        }
    }

    [JsonIgnore]
    public GridLength GraphHeight
    {
        get => new GridLength(GraphHeightInPixels, GridUnitType.Star);
        set
        {
            GraphHeightInPixels = value.Value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GraphHeight)));
        }
    }

    [JsonIgnore]
    public GridLength StatisticsHeight
    {
        get => new GridLength(StatisticsHeightInPixels, GridUnitType.Star);
        set
        {
            StatisticsHeightInPixels = value.Value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatisticsHeight)));
        }
    }

    public double GraphHeightInPixels { get; set; } = 2;

    public double StatisticsHeightInPixels { get; set; } = 1;


    public event PropertyChangedEventHandler? PropertyChanged;

    public static UiState Load()
    {
        var settings = Properties.Settings.Default;
        string json = settings.UiState;

        var defaultUiState = new UiState();

        if (string.IsNullOrEmpty(json))
        {
            return defaultUiState;
        }

        UiState? result = null;
        try
        {
            result = JsonSerializer.Deserialize<UiState>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }

        return result ?? defaultUiState;
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this);

        var settings = Properties.Settings.Default;
        settings.UiState = json;
        settings.Save();
    }

    // Internal

    bool _areHandPeakDetectorSettingsVisible = true;
    bool _areGazePeakDetectorSettingsVisible = true;
    bool _areBlinkDetectorSettingsVisible = true;
    bool _areBlinkDetector2SettingsVisible = true;
    bool _areOtherSettingsVisible = true;
    bool _isSettingsPanelVisible = true;
}
