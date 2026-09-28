namespace WhisperTranscriber;

internal static partial class Program
{
    internal static Options ParseArguments(string[] args)
    {
        string? input = null;
        var layout = InputLayout.MultiTrack;
        var totalSpeakers = 2;
        var microphoneTrack = 2;
        var desktopTrack = 3;
        var model = "large";
        int? limitSeconds = null;
        var language = "de";
        string? modelPath = null;
        string? outputDirectory = null;
        var force = false;
        var layoutsSeen = new HashSet<InputLayout>();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument.ToLowerInvariant())
            {
                case "--plain":
                    layout = InputLayout.Plain;
                    layoutsSeen.Add(layout);
                    break;
                case "--single-track":
                    layout = InputLayout.SingleTrack;
                    layoutsSeen.Add(layout);
                    break;
                case "--multi-track":
                    layout = InputLayout.MultiTrack;
                    layoutsSeen.Add(layout);
                    break;
                case "--speakers":
                    totalSpeakers = ParsePositiveInteger(args, ref index, "--speakers");
                    if (totalSpeakers is not (2 or 3))
                    {
                        throw new ArgumentException("--speakers erlaubt nur 2 oder 3.");
                    }
                    break;
                case "--mic-track":
                    microphoneTrack = ParsePositiveInteger(args, ref index, "--mic-track");
                    break;
                case "--desktop-track":
                    desktopTrack = ParsePositiveInteger(args, ref index, "--desktop-track");
                    break;
                case "--model":
                    if (++index >= args.Length)
                    {
                        throw new ArgumentException("Nach --model fehlt large oder tiny.");
                    }
                    model = args[index].ToLowerInvariant();
                    if (model is not ("large" or "tiny"))
                    {
                        throw new ArgumentException("--model erlaubt nur large oder tiny.");
                    }
                    break;
                case "--language":
                    language = ReadValue(args, ref index, argument);
                    break;
                case "--model-path":
                    modelPath = ReadValue(args, ref index, argument);
                    break;
                case "--output-dir":
                    outputDirectory = ReadValue(args, ref index, argument);
                    break;
                case "--force":
                    force = true;
                    break;
                case "--limit-seconds":
                    limitSeconds = ParsePositiveInteger(args, ref index, "--limit-seconds");
                    break;
                default:
                    if (argument.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Unbekannte Option: " + argument);
                    }
                    if (input is not null)
                    {
                        throw new ArgumentException("Es darf nur eine Eingabedatei angegeben werden.");
                    }
                    input = argument;
                    break;
            }
        }

        if (layoutsSeen.Count > 1)
        {
            throw new ArgumentException("Choose only one input layout: --plain, --single-track or --multi-track.");
        }
        if (layout == InputLayout.Plain && args.Contains("--speakers", StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("--speakers requires --single-track.");
        }
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Es fehlt eine Audio- oder Videodatei.");
        }
        if (layout == InputLayout.MultiTrack && microphoneTrack == desktopTrack)
        {
            throw new ArgumentException("Mikrofon und Desktop müssen unterschiedliche Audiospuren sein.");
        }
        if (layout == InputLayout.MultiTrack && totalSpeakers != 2)
        {
            throw new ArgumentException(
                "Mehrspur ist derzeit nur für 2 Personen vorgesehen. " +
                "Für 2 oder 3 Sprecher auf einer gemeinsamen Spur verwende --single-track.");
        }

        return new Options(
            input,
            layout,
            totalSpeakers,
            microphoneTrack,
            desktopTrack,
            model,
            limitSeconds, language, modelPath, outputDirectory, force);
    }

    internal static int ParsePositiveInteger(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || !int.TryParse(args[index], out var value) || value <= 0)
        {
            throw new ArgumentException($"{option} benötigt eine positive Ganzzahl.");
        }
        return value;
    }

    internal static bool NeedsDiarization(Options options) =>
        options.Layout == InputLayout.SingleTrack;

    internal static string GetOutputSuffix(Options options) =>
        options.Layout switch
        {
            InputLayout.Plain => "_Transcript_AMD",
            InputLayout.SingleTrack => $"_Transcript_AMD_{options.TotalSpeakers}Sprecher",
            _ => "_Transcript_AMD_Mehrspur_2Sprecher",
        };

    internal static void PrintHeader(string inputPath, Options options)
    {
        Console.WriteLine(new string('=', 76));
        Console.WriteLine("Whisper AMD/Vulkan – lokale Sprecher-Varianten");
        Console.WriteLine(new string('=', 76));
        Console.WriteLine($"Eingabe:   {inputPath}");
        Console.WriteLine($"Modell:    {options.Model}");
        Console.WriteLine($"Modus:     {options.Layout}, Sprache: {options.Language}");
        if (options.Layout != InputLayout.MultiTrack)
        {
            Console.WriteLine("Layout:    alte Aufnahme, alle Personen auf einer Audiospur");
        }
        else
        {
            Console.WriteLine($"Mikrofon:  Audiospur {options.MicrophoneTrack}");
            Console.WriteLine($"Desktop:   Audiospur {options.DesktopTrack}");
            Console.WriteLine("Gesamtmix: wird nicht verwendet");
        }
    }

    internal static void PrintUsage()
    {
        Console.WriteLine("Whisper Vulkan Transcriber (.NET 10, Windows x64)");
        Console.WriteLine("Usage: WhisperTranscriber <media> [--plain | --single-track | --multi-track]");
        Console.WriteLine("  --plain                 Transcribe first audio track without speaker labels");
        Console.WriteLine("  --single-track          CPU diarization of first track; --speakers 2|3 (default 2)");
        Console.WriteLine("  --multi-track           Label separate mic/desktop audio tracks (default mode)");
        Console.WriteLine("  --mic-track N           One-based audio stream number (default 2)");
        Console.WriteLine("  --desktop-track N       One-based audio stream number (default 3)");
        Console.WriteLine("  --language CODE         Whisper language code, or auto (default de)");
        Console.WriteLine("  --model large|tiny      Download/cache large-v3-turbo or tiny (default large)");
        Console.WriteLine("  --model-path PATH       Existing GGML model; overrides --model without download");
        Console.WriteLine("  --limit-seconds N       Positive duration limit per track");
        Console.WriteLine("  --output-dir PATH       Output directory (default beside input)");
        Console.WriteLine("  --force                 Overwrite existing output files");
    }

    internal static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--"))
            throw new ArgumentException($"{option} needs a value.");
        return args[index];
    }

}
