using System.IO;
using System.Reflection;

namespace TTNOverlay.Services;

/// <summary>
/// Lists the .wav presets embedded in the assembly (Resources\Sounds), extracting each one to a
/// real file under %AppData%\TTNOverlay\Sounds the first time it's needed. Downstream code
/// (AlertService, the settings UI's path box/browse/test) all work with plain file paths, so this
/// is the only place that has to know the presets actually live inside the .exe.
/// </summary>
public static class SoundHelper
{
    private const string ResourcePrefix = "TTNOverlay.Sounds.";

    public static List<(string Name, string FullPath)> GetAvailableSounds()
    {
        var list = new List<(string, string)>();
        var assembly = Assembly.GetExecutingAssembly();

        string extractDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TTNOverlay",
            "Sounds"
        );

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                || !resourceName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                continue;

            string fileName = resourceName[ResourcePrefix.Length..];
            string name = Path.GetFileNameWithoutExtension(fileName);

            string? extractedPath = ExtractIfNeeded(assembly, resourceName, extractDir, fileName);
            if (extractedPath is not null)
                list.Add((name, extractedPath));
        }

        return list;
    }

    private static string? ExtractIfNeeded(Assembly assembly, string resourceName, string extractDir, string fileName)
    {
        try
        {
            string destPath = Path.Combine(extractDir, fileName);
            if (File.Exists(destPath))
                return destPath;

            Directory.CreateDirectory(extractDir);

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return null;

            using var fileStream = File.Create(destPath);
            stream.CopyTo(fileStream);

            return destPath;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"SoundHelper.ExtractIfNeeded ({resourceName})", ex);
            return null;
        }
    }
}