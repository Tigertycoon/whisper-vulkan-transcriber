using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WhisperTranscriber;

internal static partial class Program
{
    internal static DiarizationEnvironment ResolveDiarizationEnvironment(string toolDirectory)
    {
        var configuredPython = Environment.GetEnvironmentVariable("WHISPER_PYTHON");
        var pythonPath = string.IsNullOrWhiteSpace(configuredPython)
            ? Path.Combine(toolDirectory, ".venv", "Scripts", "python.exe")
            : Path.GetFullPath(configuredPython);
        if (!File.Exists(pythonPath))
            throw new InvalidOperationException("Python environment missing. See docs/setup.md or set WHISPER_PYTHON.");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HF_TOKEN")))
            throw new InvalidOperationException("Set HF_TOKEN in the current process for optional diarization.");
        var scriptPath = Path.Combine(toolDirectory, "python", "diarize.py");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("Diarization script missing.", scriptPath);
        return new DiarizationEnvironment(pythonPath, scriptPath);
    }

    internal static List<TranscriptSegment> RunDiarization(
        DiarizationEnvironment environment,
        string wavPath,
        IReadOnlyList<TranscriptSegment> segments,
        int numberOfSpeakers,
        string speakerPrefix,
        string temporaryDirectory,
        string outputSpeakerJson)
    {
        var segmentsPath = Path.Combine(temporaryDirectory, "segments.json");
        var labeledSegmentsPath = Path.Combine(temporaryDirectory, "labeled-segments.json");
        File.WriteAllText(
            segmentsPath,
            JsonSerializer.Serialize(segments, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        Console.WriteLine();
        Console.WriteLine(
            $"Starte CPU-Sprecher-Erkennung: genau {numberOfSpeakers} × {speakerPrefix} ...");
        var startInfo = new ProcessStartInfo
        {
            FileName = environment.PythonPath,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false,
        };
        startInfo.Environment["PYANNOTE_METRICS_ENABLED"] = "0";
        foreach (var argument in new[]
        {
            environment.ScriptPath,
            "--audio", wavPath,
            "--segments", segmentsPath,
            "--labeled-segments", labeledSegmentsPath,
            "--speaker-json", outputSpeakerJson,
            "--num-speakers", numberOfSpeakers.ToString(CultureInfo.InvariantCulture),
            "--speaker-prefix", speakerPrefix,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Sprecher-Erkennung konnte nicht gestartet werden.");
        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(labeledSegmentsPath))
        {
            throw new InvalidOperationException(
                $"Sprecher-Erkennung ist mit Exitcode {process.ExitCode} fehlgeschlagen. " +
                "Die genaue Python-Ursache steht unmittelbar über dieser Meldung.");
        }

        var labeledSegments = JsonSerializer.Deserialize<List<TranscriptSegment>>(
            File.ReadAllText(labeledSegmentsPath, Encoding.UTF8),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (labeledSegments is null || labeledSegments.Count == 0)
        {
            throw new InvalidOperationException("Die Sprecher-Erkennung lieferte keine beschrifteten Segmente.");
        }
        return labeledSegments;
    }

}
