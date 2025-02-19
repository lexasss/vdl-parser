using System.ComponentModel;

namespace VdlParser;

public class GraphSettings : INotifyPropertyChanged, ISettings
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

    public string Section => "Graph";

    public event PropertyChangedEventHandler? PropertyChanged;

    // Internal

    bool _hasPupilSize = false;
    bool _hasPupilOpenness = false;
}
