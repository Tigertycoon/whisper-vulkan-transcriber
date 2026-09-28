using Whisper.net.Ggml;

namespace WhisperTranscriber;

internal static partial class Program
{
    internal static async Task<string> ResolveModelAsync(
        string toolDirectory,
        string modelFileName,
        GgmlType modelType,
        string? configuredPath)
    {
        if (configuredPath is not null)
        {
            var explicitPath = Path.GetFullPath(configuredPath);
            if (!IsUsableModel(explicitPath))
                throw new FileNotFoundException("Model missing or too small; supply a whisper.cpp GGML model.", explicitPath);
            return explicitPath;
        }
        var localModelsDirectory = Path.Combine(toolDirectory, "models");
        Directory.CreateDirectory(localModelsDirectory);
        var localPath = Path.Combine(localModelsDirectory, modelFileName);
        if (IsUsableModel(localPath))
        {
            return localPath;
        }

        Console.WriteLine($"Whisper-Modell fehlt und wird einmalig geladen: {modelFileName}");
        var temporaryPath = localPath + ".download";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(modelType);
        await using (var file = File.Create(temporaryPath))
        {
            await modelStream.CopyToAsync(file);
        }
        File.Move(temporaryPath, localPath, true);
        return localPath;
    }

    internal static bool IsUsableModel(string path) =>
        File.Exists(path) && new FileInfo(path).Length > 1_000_000;

}
