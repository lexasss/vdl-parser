using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
    Statistics[] _statistics = []; // log data other than VDL

    private void RefeedProcessor()
    {
        if (Vdls.SelectedItem == null)
            return;

        Processor.Feed(Vdls.SelectedItem.Records);

        if (_graphRenderer.Content == GraphContent.RawData)
        {
            _graphRenderer.DisplayRawData(Processor.HandSamples, Processor.GazeSamples);
            txbSummary.Text = null;
        }
        else if (_graphRenderer.Content == GraphContent.Processed)
        {
            _graphRenderer.DisplayProcessedData(Processor);
            txbSummary.Text = new VdlStatistics(Processor).Get(StatisticsFormat.List);
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
            Filter = "All log files|vdl-*.txt;ctt-*.txt;CTT*.csv;n-back-task-*.txt|VDL files|vdl-*.txt|CTT files|ctt-*.txt;CTT*.csv|NBack-Task files|n-back-task-*.txt",
            Multiselect = true,
        };

        if (ofd.ShowDialog() == true)
        {
            var (vdlList, statisticsList, summary) = Controller.LoadLogData(ofd.FileNames);

            txbSummary.Text = string.Join("\n\n", summary);
            _statistics = statisticsList.ToArray();
            Vdls.Add(vdlList);
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
                txbSummary.Text = null;
            }
        }
        else if (sender is ListBox lsb && lsb.SelectedItem == null)
        {
            _graphRenderer.Reset();
            txbSummary.Text = null;
        }
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        if (Vdls.SelectedItem == null)
            return;

        Processor.Feed(Vdls.SelectedItem.Records);
        _graphRenderer.DisplayProcessedData(Processor);
        txbSummary.Text = new VdlStatistics(Processor).Get(StatisticsFormat.List);
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
            txbSummary.Text = new VdlStatistics(Processor).Get(StatisticsFormat.List);
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

    private void Vdls_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Vdls.SelectedItem != null)
        {
            Vdls.Remove(Vdls.SelectedItem);
            _graphRenderer.Reset();
            txbSummary.Text = null;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(txbSummary.Text))
        {
            var wasCopied = Controller.CopySummaryToClipboard(
                Vdls, Processor, _statistics,
                Keyboard.Modifiers == ModifierKeys.Shift);

            if (wasCopied)
            {
                lblCopied.Visibility = Visibility.Visible;
                Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    Dispatcher.Invoke(() => lblCopied.Visibility = Visibility.Hidden);
                });
            }
        }
    }
}