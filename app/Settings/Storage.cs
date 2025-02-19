using System.Text.Json;

namespace VdlParser;

public static class Storage
{
    public static T Load<T>() where T : ISettings, new()
    {
        T defaultInstance = new();
        var section = defaultInstance.Section;

        var settings = Properties.Settings.Default;
        string json = (string)settings[section];

        if (string.IsNullOrEmpty(json))
        {
            return defaultInstance;
        }

        T? result = default(T);
        try
        {
            result = JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Loading {section}: {ex.Message}");
        }

        return result ?? defaultInstance;
    }

    public static void Save<T>(T instance) where T : ISettings
    {
        var json = JsonSerializer.Serialize(instance);
        Properties.Settings.Default[instance.Section] = json;
        Properties.Settings.Default.Save();
    }
}
