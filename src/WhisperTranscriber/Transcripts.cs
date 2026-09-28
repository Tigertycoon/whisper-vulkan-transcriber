using System.Text;

namespace WhisperTranscriber;

internal static partial class Program
{
    internal static List<TranscriptSegment> MergeSegments(
        IEnumerable<TranscriptSegment> first,
        IEnumerable<TranscriptSegment> second) =>
        first
            .Concat(second)
            .OrderBy(segment => segment.Start)
            .ThenBy(segment => segment.End)
            .ThenBy(segment => SpeakerOrder(segment.Speaker))
            .ToList();

    internal static int SpeakerOrder(string speaker) => speaker switch
    {
        "Mikrofon" => 0,
        "Desktop 1" => 1,
        "Desktop 2" => 2,
        "Desktop" => 3,
        _ => 10,
    };

    internal static void WriteOutputs(
        IReadOnlyList<TranscriptSegment> segments,
        string outputSrt,
        string outputTxt)
    {
        using (var srt = new StreamWriter(outputSrt, false, new UTF8Encoding(true)))
        {
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                srt.WriteLine(index + 1);
                srt.WriteLine($"{FormatSrtTimestamp(segment.Start)} --> {FormatSrtTimestamp(segment.End)}");
                srt.WriteLine(WrapSubtitle(DisplayText(segment)));
                srt.WriteLine();
            }
        }

        using var txt = new StreamWriter(outputTxt, false, new UTF8Encoding(true));
        foreach (var segment in segments)
        {
            txt.WriteLine(
                $"[{FormatConsoleTime(segment.Start)} - {FormatConsoleTime(segment.End)}] " +
                $"{DisplayText(segment)}");
        }
    }

    internal static string WrapSubtitle(string text, int width = 52)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                lines.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0)
            {
                current.Append(' ');
            }
            current.Append(word);
        }
        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }
        return string.Join(Environment.NewLine, lines);
    }

    internal static string FormatSrtTimestamp(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
    }

    internal static string FormatConsoleTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    internal static string DisplayText(TranscriptSegment segment) =>
        string.IsNullOrEmpty(segment.Speaker) ? segment.Text : $"[{segment.Speaker}] {segment.Text}";

    internal static void EnsureOutputsAvailable(string outputBase, bool force)
    {
        if (force) return;
        foreach (var suffix in new[] { ".srt", ".txt", "_Sprecher.json", "_OHNE_SPRECHER.srt", "_OHNE_SPRECHER.txt" })
            if (File.Exists(outputBase + suffix))
                throw new IOException($"Output already exists: {outputBase + suffix}. Use --force to replace it.");
    }

}
