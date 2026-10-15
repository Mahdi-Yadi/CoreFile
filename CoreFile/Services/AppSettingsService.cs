using System.IO;
using System.Text.Json;

namespace CoreFile.Services;
public class AppSettings
{
    public bool IsDarkMode { get; set; } = false;
    public List<string> PinnedFolders { get; set; } = [];
}

public static class AppSettingsService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CoreFile");

    private static readonly string ConfigFilePath = Path.Combine(AppDataFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return new AppSettings();

            string json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch
        {
            // جلوگیری از کرش در صورت اختلال در نوشتن روی دیسک
        }
    }
}