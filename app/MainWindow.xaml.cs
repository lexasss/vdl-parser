using Microsoft.Win32;
using System.IO;
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
            LoadCttCompData_Click(sender, new RoutedEventArgs());
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

    private void LoadCttCompData_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog()
        {
            Filter = "All log files|vdl-*.txt;varjo_*.csv;ctt-*.txt;CTT*.csv;n-back-task-*.txt" +
                "|VDL files|vdl-*.txt" +
                "|Varjo files|varjo_*.csv" +
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

    private void LoadHeadGazeData_Click(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select a participant folder"
        };

        if (folderDialog.ShowDialog() == true)
        {
            Vdls.Clear();
            try
            {
                var subfolders = Directory.GetDirectories(folderDialog.FolderName);
                if (subfolders.Length == 2 && subfolders[0].EndsWith("self") && subfolders[1].EndsWith("system")) 
                {
                    (var vdlList, _statistics) = Utils.LoadParticipantData(folderDialog.FolderName);

                    Vdls.Add(vdlList);

                    var summary = _statistics.Select(statistics => string.Join('\n', statistics.Get(Models.Format.List)));
                    txbSummary.Text = string.Join("\n\n", summary);
                }
                else
                {
                    var wait = new Views.Wait();
                    wait.Show();
                    Task.Run(() =>
                    {
                        Utils.CopyStatisticsToClipboard(folderDialog.FolderName);
                        Dispatcher.Invoke(() =>
                        {
                            wait.Close();
                            MessageBox.Show("Data was copied to the clipboard", Title, MessageBoxButton.OK);
                            GC.Collect();
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: '{ex.Message}'.",
                    App.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            lsbVdls.Focus();
        }
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        Processor.Process();
        graph.DisplayProcessedData(Processor);
        txbSummary.Text = new Models.VdlStatistics(Processor).Get(Models.Format.List);
    }

    private void AnalyzeParticipant_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Utils.CopyStatisticsToClipboard(Vdls.Items.ToArray());
            MessageBox.Show("Data was copied to the clipboard", Title, MessageBoxButton.OK);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: '{ex.Message}'.",
                App.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var menu = ContextMenuService.GetContextMenu(btn);
            if (menu?.IsOpen == false)
            {
                menu.HorizontalOffset = btn.ActualWidth + 5;
                menu.VerticalOffset = btn.ActualHeight;
                menu.PlacementTarget = btn;
                menu.IsOpen = true;
            }
        }
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
                : (Keyboard.Modifiers == ModifierKeys.Control
                    ? _statistics
                        .Where(s => s is Models.Nbt)
                        .ToArray()
                    : _statistics
                        .Where(s => s is Models.CttNew or Models.CttOld)
                        .ToArray());

            var wasCopied = Utils.CopyStatisticsToClipboard(statistics,
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

    private void AnalyzerSetting_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && lsbVdls.SelectedItem != null)
        {
            (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            Analyze_Click(sender, e);
        }    
    }

    private void AnalyzerSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lsbVdls.SelectedItem != null)
        {
            (sender as ComboBox)?.GetBindingExpression(ComboBox.SelectedItemProperty).UpdateSource();
            Analyze_Click(sender, e);
        }
    }
}