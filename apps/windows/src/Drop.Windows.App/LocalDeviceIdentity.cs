using System.IO;

namespace Drop.Windows;

internal static class LocalDeviceIdentity
{
    public static Guid LoadOrCreate()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Drop");
        string path = Path.Combine(directory, "device-id.txt");

        try
        {
            if (Guid.TryParse(File.ReadAllText(path).Trim(), out Guid existing) && existing != Guid.Empty)
            {
                return existing;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        Guid created = Guid.NewGuid();
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, created.ToString("D"));
        return created;
    }
}
