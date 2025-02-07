using MathNet.Numerics.Statistics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace VdlParser;

public class Controller
{
    public static (Vdl[], Statistics[], string[]) LoadLogData(string[] filenames)
    {
        var summary = new List<string>();
        var statisticsList = new List<Statistics>();
        var vdlList = new List<Vdl>();

        foreach (var filename in filenames)
        {
            bool wasParsed = false;
            var fn = Path.GetFileName(filename);

            if (fn.StartsWith("vdl-"))
            {
                var vdl = Vdl.Load(filename);
                if (vdl != null)
                {
                    vdlList.Add(vdl);
                    wasParsed = true;
                }
            }
            else
            {
                Statistics? statistics = null;
                if (fn.StartsWith("ctt-"))
                    statistics = CttNew.Load(filename);
                else if (fn.EndsWith(".csv"))
                    statistics = CttOld.Load(filename);
                else if (fn.StartsWith("n-back-task-"))
                    statistics = Nbt.Load(filename);

                wasParsed = statistics != null;
                if (statistics != null)
                {
                    summary.Add(string.Join('\n', statistics.Get(StatisticsFormat.List)));
                    statisticsList.Add(statistics);
                }
            }

            if (!wasParsed)
            {
                MessageBox.Show($"Cannot load or parse the file '{filename}'.",
                    App.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        return (
            vdlList.ToArray(),
            statisticsList.ToArray(),
            summary.ToArray()
        );
    }

    public static bool CopySummaryToClipboard(Vdls vdls, Processor processor, Statistics[] statistics, bool onlyHeaders)
    {
        string? summary = null;

        if (vdls.SelectedItem != null)
        {
            summary = new VdlStatistics(processor).Get(
                 onlyHeaders ?
                    StatisticsFormat.RowHeaders :
                    StatisticsFormat.Rows);
        }
        else if (statistics.Length > 0)
        {
            var table = new List<string[]>();
            foreach (var stat in statistics)
            {
                table.Add(stat.Get(
                        onlyHeaders ?
                            StatisticsFormat.RowHeaders :
                            StatisticsFormat.Rows)
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
