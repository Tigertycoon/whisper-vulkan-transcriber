using System.Text;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

namespace WhisperTranscriber;

internal static partial class Program
{
    internal const string LargeModelFile = "ggml-large-v3-turbo.bin";
    internal const string TinyModelFile = "ggml-tiny.bin";

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
            {
                PrintUsage();
                return 0;
            }
            var options = ParseArguments(args);
            var toolDirectory = ResolveToolDirectory();
            var inputPath = Path.GetFullPath(options.Input);
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("Eingabedatei nicht gefunden.", inputPath);
            }

            EnsureFfmpegIsAvailable();
            ValidateAudioTracks(inputPath, options);

            DiarizationEnvironment? diarizationEnvironment = null;
            if (NeedsDiarization(options))
            {
                diarizationEnvironment = ResolveDiarizationEnvironment(toolDirectory);
            }

            var outputDirectory = Path.GetFullPath(options.OutputDirectory ?? Path.GetDirectoryName(inputPath)!);
            Directory.CreateDirectory(outputDirectory);
            var outputBase = Path.Combine(
                outputDirectory,
                Path.GetFileNameWithoutExtension(inputPath) + GetOutputSuffix(options));
            var outputSrt = outputBase + ".srt";
            var outputTxt = outputBase + ".txt";
            var outputSpeakerJson = outputBase + "_Sprecher.json";
            EnsureOutputsAvailable(outputBase, options.Force);

            var modelType = options.Model == "tiny" ? GgmlType.Tiny : GgmlType.LargeV3Turbo;
            var modelFileName = options.Model == "tiny" ? TinyModelFile : LargeModelFile;
            var modelPath = await ResolveModelAsync(toolDirectory, modelFileName, modelType, options.ModelPath);

            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "WhisperAmdVarianten-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                PrintHeader(inputPath, options);

                RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan];
                using var logger = LogProvider.AddConsoleLogging(WhisperLogLevel.Info);
                using var factory = WhisperFactory.FromPath(modelPath);

                if (RuntimeOptions.LoadedLibrary != RuntimeLibrary.Vulkan)
                {
                    throw new InvalidOperationException(
                        $"Vulkan wurde nicht geladen (geladen: {RuntimeOptions.LoadedLibrary?.ToString() ?? "nichts"}). " +
                        "CPU-Fallback ist in dieser AMD-Variante absichtlich deaktiviert.");
                }

                List<TranscriptSegment> finalSegments;
                if (options.Layout != InputLayout.MultiTrack)
                {
                    finalSegments = await ProcessSingleTrackAsync(
                        factory,
                        inputPath,
                        temporaryDirectory,
                        outputBase,
                        outputSpeakerJson,
                        options,
                        diarizationEnvironment!);
                }
                else
                {
                    finalSegments = await ProcessMultiTrackAsync(
                        factory,
                        inputPath,
                        temporaryDirectory,
                        options);
                }

                if (finalSegments.Count == 0)
                {
                    throw new InvalidOperationException("Whisper hat keine Sprachsegmente erzeugt.");
                }

                WriteOutputs(finalSegments, outputSrt, outputTxt);

                Console.WriteLine();
                Console.WriteLine(new string('=', 76));
                Console.WriteLine("Fertig.");
                Console.WriteLine($"SRT: {outputSrt}");
                Console.WriteLine($"TXT: {outputTxt}");
                if (NeedsDiarization(options))
                {
                    Console.WriteLine($"Sprecher-Zeiten: {outputSpeakerJson}");
                }
                Console.WriteLine(new string('=', 76));
                return 0;
            }
            finally
            {
                TryDeleteDirectory(temporaryDirectory);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("FEHLER: " + exception.Message);
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    internal static async Task<List<TranscriptSegment>> ProcessSingleTrackAsync(
        WhisperFactory factory,
        string inputPath,
        string temporaryDirectory,
        string outputBase,
        string outputSpeakerJson,
        Options options,
        DiarizationEnvironment? diarizationEnvironment)
    {
        var wavPath = Path.Combine(temporaryDirectory, "einspur-16k-mono.wav");
        ExtractSingleTrack(inputPath, wavPath, options.LimitSeconds);
        var plainSegments = await TranscribeTrackAsync(factory, wavPath, "", options.Language);
        if (plainSegments.Count == 0 || !NeedsDiarization(options))
        {
            return plainSegments;
        }

        try
        {
            return RunDiarization(
                diarizationEnvironment!,
                wavPath,
                plainSegments,
                options.TotalSpeakers,
                "Sprecher",
                temporaryDirectory,
                outputSpeakerJson);
        }
        catch (Exception diarizationError)
        {
            var fallbackSrt = outputBase + "_OHNE_SPRECHER.srt";
            var fallbackTxt = outputBase + "_OHNE_SPRECHER.txt";
            WriteOutputs(plainSegments, fallbackSrt, fallbackTxt);
            throw new InvalidOperationException(
                "Die Transkription war erfolgreich, aber die Sprecher-Erkennung ist fehlgeschlagen. " +
                $"Das unmarkierte Ergebnis wurde gerettet: {fallbackSrt}. " +
                diarizationError.Message,
                diarizationError);
        }
    }

    internal static async Task<List<TranscriptSegment>> ProcessMultiTrackAsync(
        WhisperFactory factory,
        string inputPath,
        string temporaryDirectory,
        Options options)
    {
        var microphoneWav = Path.Combine(temporaryDirectory, "mikrofon-16k-mono.wav");
        var desktopWav = Path.Combine(temporaryDirectory, "desktop-16k-mono.wav");
        ExtractMultiTracks(
            inputPath,
            microphoneWav,
            desktopWav,
            options.MicrophoneTrack,
            options.DesktopTrack,
            options.LimitSeconds);

        var microphoneSegments = await TranscribeTrackAsync(factory, microphoneWav, "Mikrofon", options.Language);
        var desktopSegments = await TranscribeTrackAsync(factory, desktopWav, "Desktop", options.Language);
        return MergeSegments(microphoneSegments, desktopSegments);
    }

}
