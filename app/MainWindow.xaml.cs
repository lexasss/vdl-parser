using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace VdlParser;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public Controller Controller { get; } = new Controller();
    public Settings Settings { get; } = Settings.Instance;
    public bool IsSettingsPanelVisible { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
    }

    // Internal

    GraphRenderer? _graphRenderer = null;
    Processor? _processor = null;

    // UI events

    private void Window_Closed(object sender, EventArgs e)
    {
        Controller.Dispose();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog()
        {
            Filter = "VDL files|vdl-*.txt",
            Multiselect = true,
        };

        if (ofd.ShowDialog() == true)
        {
            foreach (var filename in ofd.FileNames)
            {
                var vdl = Vdl.Load(filename);
                if (vdl != null)
                {
                    Controller.Add(vdl);
                }
                else
                {
                    MessageBox.Show($"Cannot load or parse the file '{filename}'.",
                        Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void VdlsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            var vdl = e.AddedItems[0] as Vdl;
            if (vdl != null)
            {
                _processor = new Processor(vdl.Records, Controller);
                _graphRenderer = new GraphRenderer(graph);
                _graphRenderer.DisplayRawData(_processor.HandSamples, _processor.GazeSamples);
                txbSummary.Text = "";
            }
        }
        else if (sender is ListBox lsb && lsb.SelectedItem == null)
        {
            _graphRenderer?.Reset();
            _graphRenderer = null;
            txbSummary.Text = "";
        }
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        if (_processor == null)
            return;

        _graphRenderer?.DisplayProcessedData(_processor);
        txbSummary.Text = new Statistics(_processor).GetAsText();
    }

    private void PeakDetectorDataSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_processor == null)
            return;

        _processor = new Processor(_processor.Records, Controller);

        if (IsLoaded && ((ComboBox)sender).SelectedItem is GazeDataSource)
        {
            Controller.GazePeakDetector.ReversePeakSearchDirection();
        }

        if (_graphRenderer?.Content == GraphContent.RawData)
        {
            _graphRenderer.DisplayRawData(_processor.HandSamples, _processor.GazeSamples);
            txbSummary.Text = "";
        }
        else if (_graphRenderer?.Content == GraphContent.Processed)
        {
            _graphRenderer.DisplayProcessedData(_processor);
            txbSummary.Text = new Statistics(_processor).GetAsText();
        }
    }

    private void BlinkShape_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_processor == null)
            return;

        if (_graphRenderer?.Content == GraphContent.Processed)
        {
            _graphRenderer.DisplayProcessedData(_processor);
            txbSummary.Text = new Statistics(_processor).GetAsText();
        }
    }

    private void TimestampSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_processor == null)
            return;

        _processor = new Processor(_processor.Records, Controller);

        if (_graphRenderer?.Content == GraphContent.RawData)
        {
            _graphRenderer.DisplayRawData(_processor.HandSamples, _processor.GazeSamples);
            txbSummary.Text = "";
        }
        else if (_graphRenderer?.Content == GraphContent.Processed)
        {
            _graphRenderer.DisplayProcessedData(_processor);
            txbSummary.Text = new Statistics(_processor).GetAsText();
        }
    }

    private void SettingShowHide_Click(object sender, RoutedEventArgs e)
    {
        IsSettingsPanelVisible = !IsSettingsPanelVisible;
        ((Button)sender).Content = IsSettingsPanelVisible ? "🠾" : "🠼";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSettingsPanelVisible)));
    }
}