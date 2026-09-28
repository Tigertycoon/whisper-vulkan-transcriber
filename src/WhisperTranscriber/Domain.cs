namespace WhisperTranscriber;

internal enum InputLayout
{
    Plain,
    MultiTrack,
    SingleTrack,
}

internal sealed record TranscriptSegment(
    double Start,
    double End,
    string Text,
    string Speaker);

internal sealed record Options(
    string Input,
    InputLayout Layout,
    int TotalSpeakers,
    int MicrophoneTrack,
    int DesktopTrack,
    string Model,
    int? LimitSeconds,
    string Language,
    string? ModelPath,
    string? OutputDirectory,
    bool Force);

internal sealed record DiarizationEnvironment(
    string PythonPath,
    string ScriptPath);
