using System.Collections.ObjectModel;
using System.ComponentModel;
using VdlParser.Models;

namespace VdlParser;

public class Vdls : INotifyPropertyChanged
{
    public ObservableCollection<Vdl> Items { get; }
    public Vdl? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Vdls()
    {
        Items = new ObservableCollection<Vdl>(_vdls);
    }

    public void Add(Vdl vdl)
    {
        Items.Add(vdl);
    }

    public void Add(IEnumerable<Vdl> vdls)
    {
        foreach (var item in vdls)
        {
            Items.Add(item);
        }
    }

    public void Remove(Vdl vdl)
    {
        Items.Remove(vdl);
        if (vdl == SelectedItem)
        {
            SelectedItem = null;
        }
    }

    // Internal

    List<Vdl> _vdls = [];
    Vdl? _selectedItem = null;
}
