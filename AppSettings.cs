using System.Text.Json;

public record AppSettings(
bool CombineGameAudio, bool CombineMicAudio, bool CombineDiscordAudio, bool CombineMusicAudio, bool CombineDesktopAudio, bool UseMaster, bool RemoveEmptyAudioChannels
)
{
    public static AppSettings Defaults => new(false, false, false, false, false, false, false);

    private static string SettingsPath => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Autoblend", "settings.json"
    );
    public static AppSettings Load()
    {
        var path = Path.GetDirectoryName(SettingsPath)!;
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var settingsText = File.ReadAllText(SettingsPath);
        if (string.IsNullOrEmpty(settingsText))
        {
            throw new Exception("what the fuck");
        }

        var settingsJson = JsonSerializer.Deserialize<AppSettings>(settingsText);
        if (settingsJson is null)
        {
            return new(false, false, false, false, false, false, false);
        }

        return settingsJson;

    }
    public void Save()
    {
        var path = Path.GetDirectoryName(SettingsPath)!;
        try
        {

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

}
