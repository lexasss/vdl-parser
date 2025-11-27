using System.IO;
using System.Text;
using System.Windows;
using VdlParser.Models;

namespace VdlParser;

public static class Utils
{
    /// <summary>
    /// Reads and parses log files.
    /// </summary>
    /// <param name="filenames">List of file names</param>
    /// <returns>returns a tuple with 
    /// a. a list of Vdl data object, and 
    /// b. a list of other log files that share common base class to provide simple statistics only</returns>
    public static (Vdl[], IStatistics[]) LoadData(string[] filenames)
    {
        var statisticsList = new List<IStatistics>();
        var vdlList = new List<Vdl>();

        PupilCalibration? pupilCalibration = null;

        foreach (var filename in filenames)
        {
            bool wasParsed = false;
            var fn = Path.GetFileName(filename);

            if (fn.StartsWith("vdl-"))
            {
                if (fn.Contains("-calibration"))
                {
                    pupilCalibration = PupilCalibration.Load(filename);
                    wasParsed = pupilCalibration != null;
                }
                else
                {
                    var vdl = Vdl.Load(filename);
                    if (vdl != null)
                    {
                        vdlList.Add(vdl);
                        wasParsed = true;
                    }
                }
            }
            else if (fn.StartsWith("varjo_"))
            {
                var varjo = Varjo.Load(filename);
                if (varjo != null && Vdl.FromVarjo(varjo) is Vdl vdl)
                {
                    vdlList.Add(vdl);
                    wasParsed = true;
                }
            }
            else
            {
                IStatistics? statistics = null;
                if (fn.StartsWith("ctt-"))
                    statistics = CttNew.Load(filename);
                else if (fn.EndsWith(".csv"))
                    statistics = CttOld.Load(filename);
                else if (fn.StartsWith("n-back-task-"))
                    statistics = Nbt.Load(filename);

                wasParsed = statistics != null;
                if (statistics != null)
                {
                    statisticsList.Add(statistics);
                }
            }

            if (!wasParsed)
            {
                MessageBox.Show($"Cannot load or parse the file '{filename}'.",
                    App.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        foreach (var vdl in vdlList)
            vdl.PupilCalibration = pupilCalibration;

        return (
            vdlList.ToArray(),
            statisticsList.ToArray()
        );
    }

    /// <summary>
    /// Load all participant data of the HeadGaze study.
    /// The data must is stored in two folders, "self" and "system"
    /// </summary>
    /// <param name="folder">Particpant's data root folder (P??)</param>
    /// <returns>Vdl + CTT and NBT statistics</returns>
    /// <exception cref="Exception">Throws if VDL, CTT and NBT file number is not same</exception>
    public static (Vdl[], IStatistics[]) LoadParticipantData(string folder)
    {
        var statisticsList = new List<IStatistics>();
        var vdlList = new List<Vdl>();

        string[] paceFolders = [
            Path.Combine(folder, "self"),
            Path.Combine(folder, "system")
        ];

        foreach (var paceFolder in paceFolders)
        {
            var vdlFileNames = Directory.GetFiles(paceFolder, "vdl-*.txt");
            var cttFileNames = Directory.GetFiles(paceFolder, "ctt-*.txt");
            var nbtFileNames = Directory.GetFiles(paceFolder, "n-back-task-*.txt");
            var tcnFileName = Directory.GetFiles(paceFolder, "conditions-*.txt")?[0];

            if (vdlFileNames.Length != cttFileNames.Length || cttFileNames.Length != nbtFileNames.Length || tcnFileName == null)
                throw new Exception("The participant data is incomplete");

            for (int i = 0; i < vdlFileNames.Length; i++)
            {
                var vdlFilename = vdlFileNames[i];
                var cttFilename = cttFileNames[i];
                var nbtFilename = nbtFileNames[i];

                TestCondition testCondition = new TestCondition(vdlFilename, tcnFileName, i);
                var vdl = Vdl.Load(vdlFilename, testCondition);
                if (vdl != null)
                {
                    vdlList.Add(vdl);
                }
                else if (App.Current.Dispatcher.Thread == Thread.CurrentThread)
                {
                    MessageBox.Show($"Cannot load or parse the file '{vdlFilename}'.",
                        App.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (CttNew.Load(cttFilename) is IStatistics cttStatistics)
                {
                    statisticsList.Add(cttStatistics);
                }
                else if (App.Current.Dispatcher.Thread == Thread.CurrentThread)
                {
                    MessageBox.Show($"Cannot load or parse the file '{cttFilename}'.",
                        App.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (Nbt.Load(nbtFilename) is IStatistics nbtStatistics)
                {
                    statisticsList.Add(nbtStatistics);
                }
                else if (App.Current.Dispatcher.Thread == Thread.CurrentThread)
                {
                    MessageBox.Show($"Cannot load or parse the file '{nbtFilename}'.",
                        App.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        return (
            vdlList.ToArray(),
            statisticsList.ToArray()
        );
    }

    public static void CopyStatisticsToClipboard(string folder)
    {
        var folders = Directory.GetDirectories(folder, "P??");
        System.Diagnostics.Debug.WriteLine($"Loading all {folders.Length} participants");

        var statisticsList = new List<IStatistics>();

        foreach (var pf in folders)
        {
            (var vdlList, _) = LoadParticipantData(pf);
            System.Diagnostics.Debug.WriteLine($"Participant {pf.Split('\\')[^1]}");

            foreach (var vdl in vdlList)
            {
                var p = new Processor();
                p.SetVdl(vdl);
                p.Process();

                var statistics = new VdlStatistics(p);
                statisticsList.Add(statistics);
            }
        }

        var table = CreateSummary(statisticsList.ToArray(), onlyHeaders: false);

        var data = string.Join("\n", table);
        App.Current.Dispatcher?.Invoke(() =>
        {
            Clipboard.SetText(data);
        });

        System.Diagnostics.Debug.WriteLine("Done");
    }

    public static void CopyStatisticsToClipboard(Vdl[] vdls)
    {
        var statisticsList = new List<IStatistics>();

        foreach (var vdl in vdls)
        {
            var p = new Processor();
            p.SetVdl(vdl);
            p.Process();

            var statistics = new VdlStatistics(p);
            statisticsList.Add(statistics);
        }

        var table = CreateSummary(statisticsList.ToArray(), onlyHeaders: false);

        var data = string.Join("\n", table);
        App.Current.Dispatcher?.Invoke(() =>
        {
            Clipboard.SetText(data);
        });
    }

    public static bool CopyStatisticsToClipboard(IStatistics[] statistics, bool onlyHeaders)
    {
        string? summary = null;

        if (statistics.Length > 0)
        {
            summary = string.Join("\n", CreateSummary(statistics, onlyHeaders));
        }
        else
        {
            return false;
        }

        Clipboard.SetText(summary);
        return true;
    }


    // Internal

    private static string[] CreateSummary(IStatistics[] statistics, bool onlyHeaders)
    {
        var table = new List<string[]>();
        foreach (var stat in statistics)
        {
            table.Add(stat.Get(
                    onlyHeaders ?
                        Format.RowHeaders :
                        Format.Rows)
                .Split('\n')
                .ToArray()
            );

            if (onlyHeaders)
                break;
        }

        var rowCount = table.Max(col => col.Length);
        var formattedTable = new StringBuilder[rowCount];
        for (int row = 0; row < formattedTable.Length; row++)
            formattedTable[row] = new StringBuilder();

        for (int col = 0; col < table.Count; col++)
        {
            var column = table[col];
            for (int row = 0; row < column.Length; row++)
            {
                if (col > 0)
                    formattedTable[row].Append("\t");

                var value = column[row];
                if (value == "." || value == "NaN")
                    value = "";
                formattedTable[row].Append(value);
            }
        }

        return formattedTable.Select(sb => string.Join("\t", sb)).ToArray();
    }
}
