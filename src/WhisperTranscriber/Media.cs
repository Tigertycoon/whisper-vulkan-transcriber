using System.Diagnostics;
using System.Globalization;

namespace WhisperTranscriber;

internal static partial class Program
{
    internal static void EnsureFfmpegIsAvailable()
    {
        foreach (var executable in new[] { "ffmpeg", "ffprobe" })
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null) throw new InvalidOperationException($"Cannot start {executable}.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{executable} wurde nicht gefunden. Bitte ffmpeg installieren und in PATH aufnehmen.");
            }
        }
    }

    internal static void ValidateAudioTracks(string inputPath, Options options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "-v", "error",
            "-select_streams", "a",
            "-show_entries", "stream=index",
            "-of", "csv=p=0",
            inputPath,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("ffprobe konnte nicht gestartet werden.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var standardOutput = stdoutTask.GetAwaiter().GetResult();
        var standardError = stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe konnte die Datei nicht lesen: {standardError.Trim()}");
        }

        var audioTrackCount = standardOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Length;
        var requiredTrack = options.Layout != InputLayout.MultiTrack
            ? 1
            : Math.Max(options.MicrophoneTrack, options.DesktopTrack);
        if (audioTrackCount < requiredTrack)
        {
            throw new InvalidOperationException(
                $"Die Datei enthält nur {audioTrackCount} Audiospur(en), benötigt wird mindestens Spur {requiredTrack}. " +
                "Mehrspur-Standard: Spur 2 = Mikrofon, Spur 3 = Desktop.");
        }

        Console.WriteLine($"Gefundene Audiospuren: {audioTrackCount}");
    }

    internal static void ExtractSingleTrack(
        string inputPath,
        string wavPath,
        int? limitSeconds)
    {
        Console.WriteLine("Bereite die gemeinsame Audiospur mit ffmpeg vor ...");
        var startInfo = CreateFfmpegStartInfo(inputPath);
        AddWavOutput(startInfo, 1, wavPath, limitSeconds);
        RunFfmpeg(startInfo, wavPath);
    }

    internal static void ExtractMultiTracks(
        string inputPath,
        string microphoneWav,
        string desktopWav,
        int microphoneTrack,
        int desktopTrack,
        int? limitSeconds)
    {
        Console.WriteLine("Extrahiere Mikrofon- und Desktopspur gemeinsam mit ffmpeg ...");
        var startInfo = CreateFfmpegStartInfo(inputPath);
        AddWavOutput(startInfo, microphoneTrack, microphoneWav, limitSeconds);
        AddWavOutput(startInfo, desktopTrack, desktopWav, limitSeconds);
        RunFfmpeg(startInfo, microphoneWav, desktopWav);
    }

    internal static ProcessStartInfo CreateFfmpegStartInfo(string inputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false,
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "warning", "-y", "-i", inputPath,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }
        return startInfo;
    }

    internal static void AddWavOutput(
        ProcessStartInfo startInfo,
        int userFacingTrackNumber,
        string outputPath,
        int? limitSeconds)
    {
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add($"0:a:{userFacingTrackNumber - 1}");
        if (limitSeconds is not null)
        {
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(limitSeconds.Value.ToString(CultureInfo.InvariantCulture));
        }
        startInfo.ArgumentList.Add("-vn");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("16000");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("pcm_s16le");
        startInfo.ArgumentList.Add(outputPath);
    }

    internal static void RunFfmpeg(ProcessStartInfo startInfo, params string[] expectedFiles)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("ffmpeg konnte nicht gestartet werden.");
        process.WaitForExit();
        if (process.ExitCode != 0 || expectedFiles.Any(path => !File.Exists(path)))
        {
            throw new InvalidOperationException($"ffmpeg ist mit Exitcode {process.ExitCode} fehlgeschlagen.");
        }
    }

}
