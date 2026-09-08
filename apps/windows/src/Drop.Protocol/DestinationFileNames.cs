namespace Drop.Protocol;

/// <summary>
/// Converts untrusted protocol labels into safe, unique destination paths.
/// </summary>
public static class DestinationFileNames
{
    private const int MaximumFileNameLength = 240;

    public static string Sanitize(string incomingName)
    {
        ArgumentNullException.ThrowIfNull(incomingName);

        string[] components = incomingName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        string name = components.Length == 0 ? string.Empty : components[^1];
        HashSet<char> invalid = [.. Path.GetInvalidFileNameChars(), '/', '\\'];
        name = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray())
            .Trim()
            .TrimEnd('.');

        if (name is "" or "." or "..")
        {
            name = "unnamed";
        }

        string stem = Path.GetFileNameWithoutExtension(name);
        if (IsReservedWindowsName(stem))
        {
            name = $"_{name}";
        }

        if (name.Length > MaximumFileNameLength)
        {
            string extension = Path.GetExtension(name);
            string nameStem = Path.GetFileNameWithoutExtension(name);
            if (extension.Length >= MaximumFileNameLength || nameStem.Length == 0)
            {
                name = name[..MaximumFileNameLength].TrimEnd('.');
            }
            else
            {
                int stemLength = Math.Min(nameStem.Length, MaximumFileNameLength - extension.Length);
                name = $"{nameStem[..stemLength]}{extension}";
            }
        }

        return name;
    }

    public static string ResolveUniquePath(string destinationDirectory, string incomingName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        string directory = Path.GetFullPath(destinationDirectory);
        string safeName = Sanitize(incomingName);
        string extension = Path.GetExtension(safeName);
        string stem = Path.GetFileNameWithoutExtension(safeName);

        for (int suffix = 0; ; suffix++)
        {
            string candidateName = suffix == 0 ? safeName : $"{stem} ({suffix}){extension}";
            string candidate = Path.GetFullPath(Path.Combine(directory, candidateName));
            EnsureInsideDirectory(directory, candidate);
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    internal static void EnsureInsideDirectory(string directory, string path)
    {
        string directoryWithSeparator = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))
            + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(directoryWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Resolved destination escapes the destination directory.");
        }
    }

    private static bool IsReservedWindowsName(string stem)
    {
        string upper = stem.ToUpperInvariant();
        return upper is "CON" or "PRN" or "AUX" or "NUL" or
            "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or
            "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9";
    }
}
