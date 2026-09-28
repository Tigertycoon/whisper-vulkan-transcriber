using System.Diagnostics;
using Whisper.net;

namespace WhisperTranscriber;

internal static partial class Program
{
    internal static async Task<List<TranscriptSegment>> TranscribeTrackAsync(
        WhisperFactory factory,
        string wavPath,
        string speaker,
        string language)
    {
        Console.WriteLine();
        Console.WriteLine($"Transkribiere {speaker} auf Vulkan ...");
        using var processor = factory.CreateBuilder()
            .WithLanguage(language)
            .WithBeamSearchSamplingStrategy(strategy => strategy.WithBeamSize(5))
            .Build();
        await using var audio = File.OpenRead(wavPath);

        var segments = new List<TranscriptSegment>();
        var stopwatch = Stopwatch.StartNew();
        await foreach (var result in processor.ProcessAsync(audio))
        {
            var text = result.Text.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            var segment = new TranscriptSegment(
                result.Start.TotalSeconds,
                result.End.TotalSeconds,
                text,
                speaker);
            segments.Add(segment);
            Console.WriteLine(
                $"[{FormatConsoleTime(segment.Start)} - {FormatConsoleTime(segment.End)}] " +
                $"{DisplayText(segment)}");
        }

        stopwatch.Stop();
        Console.WriteLine(
            $"{speaker} fertig nach {stopwatch.Elapsed.TotalMinutes:F1} Minuten " +
            $"({segments.Count} Segmente).");
        return segments;
    }

}
