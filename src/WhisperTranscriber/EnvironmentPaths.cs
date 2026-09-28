namespace WhisperTranscriber;

internal static partial class Program
{
    internal static string ResolveToolDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("WHISPER_TRANSCRIBER_HOME");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        var candidate = new DirectoryInfo(AppContext.BaseDirectory);
        while (candidate is not null)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "WhisperTranscriber.slnx")))
            {
                return candidate.FullName;
            }
            candidate = candidate.Parent;
        }

        return AppContext.BaseDirectory;
    }

    internal static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Hinweis: Temporärer Ordner konnte nicht entfernt werden: {exception.Message}");
        }
    }
}
