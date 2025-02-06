using System.Collections.ObjectModel;

namespace VdlParser;

public class Vdls
{
    public ObservableCollection<Vdl> Items { get; }
    public Vdl? SelectedItem { get; set; } = null;

    public Vdls()
    {
        Items = new ObservableCollection<Vdl>(_vdls);
    }

    public void Add(Vdl vdl)
    {
        Items.Add(vdl);
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
}
