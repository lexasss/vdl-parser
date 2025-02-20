using System.IO;
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

    public static bool CopySummaryToClipboard(IStatistics[] statistics, bool onlyHeaders)
    {
        string? summary = null;

        if (statistics.Length > 0)
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
            var formattedTable = new string[rowCount, table.Count];
            for (int col = 0; col < table.Count; col++)
            {
                var column = table[col];
                for (int row = 0; row < column.Length; row++)
                    formattedTable[row, col] = column[row];
            }

            var result = new List<object>();
            var index = 0;
            foreach (var el in formattedTable)
                result.AddRange([el, (++index % statistics.Length) == 0 ? '\n' : '\t']);

            summary = string.Join("", result);
        }
        else
        {
            return false;
        }

        Clipboard.SetText(summary);
        return true;
    }
}
