using System.Windows;
using System.Windows.Controls;

namespace VdlParser;

public partial class MainWindow : Window
{
    public Controller Controller { get; } = new Controller();

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
    }

    // Internal


    // UI events

    private void Window_Closed(object sender, EventArgs e)
    {
        Controller.Dispose();
    }

    private void VdlsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            var vdl = e.AddedItems[0] as Vdl;
            if (vdl != null)
            {
                Controller.Display(vdl, graph);
                txbSummary.Text = "";
            }
        }
        else if (sender is ListBox lsb && lsb.SelectedItem == null)
        {
            Controller.Reset(graph);
            txbSummary.Text = "";
        }
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
                    MessageBox.Show($"Cannot load or parse the file '{filename}'.", Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void ApplyPeakDetector_Click(object sender, RoutedEventArgs e)
    {
        txbSummary.Text = Controller.AnalyzeAndDraw((Vdl)lsbVdls.SelectedItem, graph);
    }

    private void PeakDetectorDataSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && ((ComboBox)sender).SelectedItem is GazeDataSource)
        {
            Controller.GazePeakDetector.ReversePeakSearchDirection();
        }

        if (Controller.State == ControllerState.DataDisplayed)
        {
            Controller.Display((Vdl)lsbVdls.SelectedItem, graph);
            txbSummary.Text = "";
        }
        else if (Controller.State == ControllerState.DataProcessed)
        {
            txbSummary.Text = Controller.AnalyzeAndDraw((Vdl)lsbVdls.SelectedItem, graph);
        }
    }

    private void BlinkShape_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Controller.State == ControllerState.DataProcessed)
        {
            txbSummary.Text = Controller.AnalyzeAndDraw((Vdl)lsbVdls.SelectedItem, graph);
        }
    }

    private void TimestampSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Controller.State == ControllerState.DataDisplayed)
        {
            Controller.Display((Vdl)lsbVdls.SelectedItem, graph);
            txbSummary.Text = "";
        }
        else if (Controller.State == ControllerState.DataProcessed)
        {
            txbSummary.Text = Controller.AnalyzeAndDraw((Vdl)lsbVdls.SelectedItem, graph);
        }
    }
}