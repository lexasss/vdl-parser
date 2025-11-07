using System.IO;

namespace VdlParser.Models;

public class TestCondition
{
    public string Participant { get; }
    public string Pace { get; }
    public string Layout { get; }
    public int Digits { get; }
    public double Lambda { get; }

    public TestCondition()
    {
        Participant = string.Empty;
        Pace = string.Empty;
        Layout = string.Empty;
        Digits = 0;
        Lambda = 0;
    }

    public TestCondition(string vdlFilename, string? conditionsFilename, int index = -1) : this()
    {
        if (string.IsNullOrWhiteSpace(vdlFilename))
        {
            return;
        }

        if (!string.IsNullOrEmpty(conditionsFilename) && File.Exists(conditionsFilename))
        {
            var conditions = File
                .ReadAllLines(conditionsFilename)
                .Skip(1)
                .Select(line =>
                {
                    var p = line.Split('\t');
                    return new Condition(double.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]));
                })
                .ToArray();
            var condition = conditions[index];
            Layout = condition.Layout == 2 ? "random" : "fixed";
            Digits = condition.Digits;
            Lambda = condition.Lambda;
        }
        else
        {
            var newCttFilename = Utils.GetCorrespondingNewCtt(vdlFilename);
            Lambda = newCttFilename != null ? Utils.GetLambda(newCttFilename) : 0;
        }

        string participant = string.Empty;

        var folders = Path.GetDirectoryName(vdlFilename)?.Split(Path.DirectorySeparatorChar);
        if (folders != null && folders.Length > 2)
        {
            if (folders[^2][0] == 'P')
            {
                Participant = folders[^2];
                Pace = folders[^1];
            }
            else if (folders[^3][0] == 'P')
            {
                Participant = folders[^3];
            }
        }
    }

    // Internal

    record class Condition(double Lambda, int Digits, int Layout);
}
