using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace VdlParser;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public Vdls Vdls { get; } = new Vdls();
    public Processor Processor { get; } = new Processor();
    public Settings Settings { get; } = Settings.Instance;
    public bool IsSettingsPanelVisible { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;

        _graphRenderer = new GraphRenderer(graph);
    }

    // Internal

    GraphRenderer _graphRenderer;

    private void RefeedProcessor()
    {
        if (Vdls.SelectedItem == null)
            return;

        Processor.Feed(Vdls.SelectedItem.Records);

        if (_graphRenderer.Content == GraphContent.RawData)
        {
            _graphRenderer.DisplayRawData(Processor.HandSamples, Processor.GazeSamples);
            txbSummary.Text = "";
        }
        else if (_graphRenderer.Content == GraphContent.Processed)
        {
            _graphRenderer.DisplayProcessedData(Processor);
            txbSummary.Text = new Statistics(Processor).GetAsText();
        }
    }

    // UI events

    private void Window_Closed(object sender, EventArgs e)
    {
        Processor.Dispose();
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
                    Vdls.Add(vdl);
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
                Processor.Feed(vdl.Records);
                _graphRenderer.DisplayRawData(Processor.HandSamples, Processor.GazeSamples);
                txbSummary.Text = "";
            }
        }
        else if (sender is ListBox lsb && lsb.SelectedItem == null)
        {
            _graphRenderer.Reset();
            txbSummary.Text = "";
        }
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        _graphRenderer.DisplayProcessedData(Processor);
        txbSummary.Text = new Statistics(Processor).GetAsText();
    }

    private void PeakDetectorDataSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && ((ComboBox)sender).SelectedItem is GazeDataSource)
        {
            Processor.GazePeakDetector.ReversePeakSearchDirection();
        }

        RefeedProcessor();
    }

    private void BlinkShape_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_graphRenderer.Content == GraphContent.Processed)
        {
            _graphRenderer.DisplayProcessedData(Processor);
            txbSummary.Text = new Statistics(Processor).GetAsText();
        }
    }

    private void TimestampSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefeedProcessor();
    }

    private void SettingShowHide_Click(object sender, RoutedEventArgs e)
    {
        IsSettingsPanelVisible = !IsSettingsPanelVisible;
        ((Button)sender).Content = IsSettingsPanelVisible ? "🠾" : "🠼";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSettingsPanelVisible)));
    }

    private void Vdls_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete && Vdls.SelectedItem != null)
        {
            Vdls.Remove(Vdls.SelectedItem);
            _graphRenderer.Reset();
            txbSummary.Text = "";
        }
    }
}