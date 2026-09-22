using System.Text.Json;

namespace JasoWatch;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public string WatchFolder { get; set; } = KnownFolders.Downloads;
    public bool IncludeSubdirectories { get; set; } = true;
    public List<string> ExcludedExtensions { get; set; } = [".crdownload", ".part", ".tmp"];
    public List<string> ExcludedPrefixes { get; set; } = ["~$"];

    public static AppSettings Defaults() => new();
}

public static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JasoWatch");
    public static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return AppSettings.Defaults();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? AppSettings.Defaults();
        }
        catch
        {
            try { File.Move(FilePath, FilePath + ".bak", true); } catch { }
            return AppSettings.Defaults();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public static class KnownFolders
{
    public static string Downloads
    {
        get
        {
            try
            {
                var path = SHGetKnownFolderPath();
                if (!string.IsNullOrWhiteSpace(path)) return path;
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint flags, IntPtr token, out IntPtr path);

    private static string? SHGetKnownFolderPath()
    {
        var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        var result = SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var path);
        if (result != 0) return null;
        try { return System.Runtime.InteropServices.Marshal.PtrToStringUni(path); }
        finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(path); }
    }
}
