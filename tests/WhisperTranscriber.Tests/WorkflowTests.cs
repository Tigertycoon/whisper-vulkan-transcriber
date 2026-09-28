using System.Diagnostics;
using WhisperTranscriber;

namespace WhisperTranscriber.Tests;

public sealed class WorkflowTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "transcriber-tests-" + Guid.NewGuid().ToString("N"));

    public WorkflowTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void MultiTrackDefaultsPreserveRecordingLayout()
    {
        var options = Program.ParseArguments(["recording.mkv"]);
        Assert.Equal(InputLayout.MultiTrack, options.Layout);
        Assert.Equal(2, options.MicrophoneTrack);
        Assert.Equal(3, options.DesktopTrack);
        Assert.False(Program.NeedsDiarization(options));
    }

    [Theory]
    [InlineData("--plain", false)]
    [InlineData("--single-track", true)]
    public void SingleStreamModesMakeDiarizationExplicit(string mode, bool diarization)
    {
        var options = Program.ParseArguments(["input.wav", mode]);
        Assert.Equal(diarization, Program.NeedsDiarization(options));
    }

    [Theory]
    [InlineData("--speakers", "3")]
    [InlineData("--mic-track", "3")]
    [InlineData("--limit-seconds", "0")]
    [InlineData("--speakers", "4")]
    [InlineData("--model", "unknown")]
    [InlineData("--language", "--force")]
    [InlineData("--plain", "--multi-track")]
    [InlineData("--plain", "--single-track")]
    public void InvalidModesFailBeforeMediaOrModelAccess(string first, string second) =>
        Assert.Throws<ArgumentException>(() => Program.ParseArguments(["input.wav", first, second]));

    [Fact]
    public void PathsWithSpacesAndThreeSpeakerModeAreParsed()
    {
        var options = Program.ParseArguments(["recording with spaces.mkv", "--single-track", "--speakers", "3",
            "--model-path", "models/custom model.bin", "--output-dir", "output folder", "--language", "en"]);
        Assert.Equal(3, options.TotalSpeakers);
        Assert.Equal("models/custom model.bin", options.ModelPath);
        Assert.Equal("output folder", options.OutputDirectory);
        Assert.Equal("en", options.Language);
    }

    [Fact]
    public void MergeKeepsOverlappingSpeechAndStableTrackOrder()
    {
        var microphone = new[] { new TranscriptSegment(4, 6, "mic late", "Mikrofon"), new TranscriptSegment(0, 3, "mic", "Mikrofon") };
        var desktop = new[] { new TranscriptSegment(0, 3, "desktop", "Desktop"), new TranscriptSegment(2, 5, "overlap", "Desktop") };
        var result = Program.MergeSegments(microphone, desktop);
        Assert.Equal(new[] { "mic", "desktop", "overlap", "mic late" }, result.Select(s => s.Text));
        Assert.Equal(3, result[0].End);
        Assert.Equal(2, result[2].Start);
    }

    [Fact]
    public void OutputsRetainUnicodeHoursAndPlainTextWithoutEmptyLabels()
    {
        var srt = Path.Combine(directory, "result.srt");
        var txt = Path.Combine(directory, "result.txt");
        Program.WriteOutputs([new(3601.125, 3603.250, "Grüße aus Köln.", "")], srt, txt);
        Assert.Contains("01:00:01,125 --> 01:00:03,250", File.ReadAllText(srt));
        Assert.Contains("Grüße aus Köln.", File.ReadAllText(txt));
        Assert.DoesNotContain("[]", File.ReadAllText(srt));
    }

    [Fact]
    public void ExistingTranscriptRequiresExplicitOverwrite()
    {
        var outputBase = Path.Combine(directory, "existing");
        File.WriteAllText(outputBase + "_OHNE_SPRECHER.txt", "keep me");
        Assert.Throws<IOException>(() => Program.EnsureOutputsAvailable(outputBase, false));
        Program.EnsureOutputsAvailable(outputBase, true);
        Assert.Equal("keep me", File.ReadAllText(outputBase + "_OHNE_SPRECHER.txt"));
    }

    [Fact]
    public async Task MissingExplicitModelDoesNotDownloadAnything()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => Program.ResolveModelAsync(directory,
            "ggml-tiny.bin", Whisper.net.Ggml.GgmlType.Tiny, Path.Combine(directory, "missing.bin")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
    }

    [Fact]
    public void FfmpegExtractsSelectedStreamsAndRejectsInsufficientStreams()
    {
        var input = Path.Combine(directory, "three streams with spaces.mkv");
        var first = Path.Combine(directory, "microphone.wav");
        var second = Path.Combine(directory, "desktop.wav");
        // Different tones prove stream selection; the first stream is an unused mix.
        Run("ffmpeg", "-v", "error", "-f", "lavfi", "-i", "sine=frequency=220:duration=1",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-f", "lavfi", "-i", "sine=frequency=880:duration=1",
            "-map", "0:a", "-map", "1:a", "-map", "2:a", "-c:a", "pcm_s16le", input);
        var options = Program.ParseArguments([input]);
        Program.ValidateAudioTracks(input, options);
        Program.ExtractMultiTracks(input, first, second, 2, 3, 1);
        var firstPcm = File.ReadAllBytes(first);
        var secondPcm = File.ReadAllBytes(second);
        Assert.NotEqual(firstPcm, secondPcm);
        var samples1 = ReadPcm(first);
        var samples2 = ReadPcm(second);
        Assert.InRange(ZeroCrossingFrequency(samples1), 435, 445);
        Assert.InRange(ZeroCrossingFrequency(samples2), 875, 885);
        Assert.Throws<InvalidOperationException>(() => Program.ValidateAudioTracks(first, options));
        Program.ValidateAudioTracks(first, Program.ParseArguments([first, "--plain"]));
    }

    private static short[] ReadPcm(string input)
    {
        var raw = input + ".raw";
        Run("ffmpeg", "-v", "error", "-i", input, "-f", "s16le", "-acodec", "pcm_s16le", raw);
        var bytes = File.ReadAllBytes(raw);
        Assert.InRange(bytes.Length, 31990, 32010);
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
        return samples;
    }

    private static double ZeroCrossingFrequency(short[] samples)
    {
        var crossings = 0;
        for (var i = 1; i < samples.Length; i++)
            if (samples[i - 1] <= 0 && samples[i] > 0) crossings++;
        return crossings / (samples.Length / 16000.0);
    }

    private static void Run(string executable, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        Assert.True(process.WaitForExit(30_000), "Media fixture process timed out.");
        Assert.Equal(0, process.ExitCode);
    }

    public void Dispose() => Directory.Delete(directory, true);
}
