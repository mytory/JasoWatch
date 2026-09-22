using System.Text;

namespace JasoWatch;

public static class FilenameNormalizer
{
    public static bool NeedsNormalization(string name) => !StringComparer.Ordinal.Equals(name, name.Normalize(NormalizationForm.FormC));

    public static bool IsExcludedFile(string path, AppSettings settings)
    {
        var name = Path.GetFileName(path);
        if (settings.ExcludedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return true;
        var extension = Path.GetExtension(name);
        return settings.ExcludedExtensions.Any(excluded => string.Equals(NormalizeExtension(excluded), extension, StringComparison.OrdinalIgnoreCase));
    }

    public static string FindAvailableName(string directory, string desiredName, Func<string, bool>? exists = null)
    {
        exists ??= candidate => File.Exists(Path.Combine(directory, candidate)) || Directory.Exists(Path.Combine(directory, candidate));
        if (!exists(desiredName)) return desiredName;
        var extension = Path.GetExtension(desiredName);
        var stem = extension.Length > 0 ? desiredName[..^extension.Length] : desiredName;
        for (var number = 1; ; number++)
        {
            var candidate = $"{stem} {number}{extension}";
            if (!exists(candidate)) return candidate;
        }
    }

    public static bool IsWithinScope(string path, string root, bool includeSubdirectories)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(fullPath);
        return includeSubdirectories
            ? fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            : string.Equals(parent, fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string extension) => extension.StartsWith('.') ? extension : "." + extension;
}
