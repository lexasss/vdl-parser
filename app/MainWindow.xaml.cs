using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VdlParser;

public partial class MainWindow : Window
{
    public Vdls Vdls { get; } = new Vdls();
    public Processor Processor { get; } = new Processor();
    public GeneralSettings Settings { get; } = GeneralSettings.Instance;
    public UiState UiState { get; } = Storage.Load<UiState>();
    public Controls.GraphSettings GraphSettings => graph.Settings;

    public MainWindow()
    {
        InitializeComponent();

        GraphSettings.PropertyChanged += (s, e) => RefeedProcessor();
    }

    // Internal

    Models.IStatistics[] _statistics = []; // log data other than VDL

    private void RefeedProcessor()
    {
        if (Vdls.SelectedItem == null)
            return;

        Processor.SetVdl(Vdls.SelectedItem);

        if (graph.DisplayState == Controls.GraphDisplayState.RawData)
        {
            graph.Reset();
            graph.AddRawData(Processor);
            graph.Render();
            txbSummary.Text = null;
        }
        else if (graph.DisplayState == Controls.GraphDisplayState.ProcessedData)
        {
            Processor.Process();

            graph.DisplayProcessedData(Processor);
            txbSummary.Text = new Models.VdlStatistics(Processor).Get(Models.Format.List);
        }
    }

    // UI events

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            Load_Click(sender, new RoutedEventArgs());
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        Processor.SaveSettings();
        Storage.Save(UiState);
        Storage.Save(GraphSettings);
    }

    private void Vdls_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            var vdl = e.AddedItems[0] as Models.Vdl;
            if (vdl != null)
            {
                Processor.SetVdl(vdl);
                graph.Reset();
                graph.AddRawData(Processor);
                graph.Render();
                txbSummary.Text = null;
            }
        }
        else if (sender is ListBox lsb && lsb.SelectedItem == null)
        {
            graph.Reset();
            txbSummary.Text = null;
        }
    }

    private void Vdls_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Vdls.SelectedItem != null)
        {
            Vdls.Remove(Vdls.SelectedItem);
            graph.Reset();
            txbSummary.Text = null;
        }
        else if (e.Key == Key.Enter && Vdls.SelectedItem != null)
        {
            Analyze_Click(this, new RoutedEventArgs());
        }
    }

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog()
        {
            Filter = "All log files|vdl-*.txt;ctt-*.txt;CTT*.csv;n-back-task-*.txt" +
                "|VDL files|vdl-*.txt" +
                "|CTT files|ctt-*.txt;CTT*.csv" +
                "|NBack-Task files|n-back-task-*.txt",
            Multiselect = true,
        };

        if (ofd.ShowDialog() == true)
        {
            Vdls.SelectedItem = null;

            (var vdlList, _statistics) = Utils.LoadData(ofd.FileNames);

            Vdls.Add(vdlList);

            var summary = _statistics.Select(statistics => string.Join('\n', statistics.Get(Models.Format.List)));
            txbSummary.Text = string.Join("\n\n", summary);

            lsbVdls.Focus();
        }
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        Processor.Process();
        graph.DisplayProcessedData(Processor);
        txbSummary.Text = new Models.VdlStatistics(Processor).Get(Models.Format.List);
    }

    private void TimestampSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefeedProcessor();
    }

    private void PeakDetectorDataSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && ((ComboBox)sender).SelectedItem is GazeDataSource)
        {
            Processor.GazePeakDetector.ReversePeakSearchDirection();
        }

        RefeedProcessor();
    }

    private void SettingShowHide_Click(object sender, RoutedEventArgs e)
    {
        UiState.IsSettingsPanelVisible = !UiState.IsSettingsPanelVisible;
        ((Button)sender).Content = UiState.IsSettingsPanelVisible ? "🠾" : "🠼";
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(txbSummary.Text))
        {
            var statistics = Vdls.SelectedItem != null
                ? [new Models.VdlStatistics(Processor)]
                : _statistics;

            var wasCopied = Utils.CopySummaryToClipboard(statistics,
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