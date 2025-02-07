using System.Windows;

namespace VdlParser;

public partial class App : Application
{
    public const int TRAINING_TRIAL_COUNT = 2;
    public const int VALID_TRIAL_COUNT = 20;
    public const int TRIAL_DURATION = 3;    // seconds

    public static DateTime ParseDateTime(string[] str) =>
        new DateTime(
            int.Parse(str[0]), int.Parse(str[1]), int.Parse(str[2]),
            int.Parse(str[3]), int.Parse(str[4]), int.Parse(string.Join("", str[5].SkipLast(1))));
}
