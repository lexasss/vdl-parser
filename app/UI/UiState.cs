using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;

namespace VdlParser;

public class UiState : INotifyPropertyChanged, ISettings
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

    public string Section => nameof(UiState);

    public event PropertyChangedEventHandler? PropertyChanged;

    // Internal

    bool _areHandPeakDetectorSettingsVisible = true;
    bool _areGazePeakDetectorSettingsVisible = true;
    bool _areBlinkDetectorSettingsVisible = true;
    bool _areBlinkDetector2SettingsVisible = true;
    bool _areOtherSettingsVisible = true;
    bool _isSettingsPanelVisible = true;
}
