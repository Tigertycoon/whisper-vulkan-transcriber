# Whisper Vulkan Transcriber

[![CI](https://github.com/Tigertycoon/whisper-vulkan-transcriber/actions/workflows/ci.yml/badge.svg)](https://github.com/Tigertycoon/whisper-vulkan-transcriber/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Local audio/video transcription for Windows, written in **C#/.NET** with a **Vulkan inference backend**. Designed around an AMD workstation and recordings with separate microphone and desktop audio streams. Exports timestamped TXT and SRT subtitles; optionally assigns speaker labels to a mixed recording through Python/pyannote.

This is my application and workflow integration around existing speech models. It does not implement or train Whisper or pyannote.

## Three workflows

| Input | Processing | Output labels |
| --- | --- | --- |
| Ordinary audio or video | First audio stream → Whisper/Vulkan | Plain transcript |
| Separate microphone and desktop streams | Each selected stream → Whisper/Vulkan → chronological merge | `Mikrofon`, `Desktop` |
| All speakers on one stream | Whisper/Vulkan + optional pyannote/CPU | `Sprecher 1`, `Sprecher 2`, optionally `Sprecher 3` |

The multitrack default expects **audio stream 1 = mix, 2 = microphone, 3 = desktop**. Stream numbers are one-based audio stream indices, not left/right stereo channels. The desktop label identifies a source, not an individual person.

## Quick start

Requires **Windows 11 x64, .NET 10 SDK, FFmpeg/ffprobe on PATH, a working Vulkan GPU driver and the Visual C++ x64 runtime**. The local GPU check used an AMD Radeon RX 6950 XT. Other GPUs and operating systems have not been validated for this application.

```powershell
git clone https://github.com/Tigertycoon/whisper-vulkan-transcriber.git
cd whisper-vulkan-transcriber
dotnet restore --locked-mode
dotnet run --project src/WhisperTranscriber -c Release -- "C:\Media\recording.m4a" --plain
```

The first run downloads `large-v3-turbo` (about 1.6 GB) to `models/`. For a smaller smoke test, add `--model tiny`. Language defaults to German; use `--language en` or `--language auto`. Existing compatible GGML models can be supplied with `--model-path "C:\Models\ggml-tiny.bin"`, avoiding a download.

For a multitrack recording:

```powershell
dotnet run --project src/WhisperTranscriber -c Release -- "C:\Media\recording.mkv" --multi-track
```

For optional mixed-audio speaker assignment, follow the [Python setup](docs/setup.md#optional-speaker-assignment) first:

```powershell
dotnet run --project src/WhisperTranscriber -c Release -- "C:\Media\conversation.wav" --single-track --speakers 2
```

Windows drag-and-drop launchers are included: `Transcribe.bat`, `MicrophoneAndDesktop.bat`, `TwoSpeakers.bat`, `ThreeSpeakers.bat`. Each accepts one file and builds incrementally before running.

Outputs are saved beside the input. Use `--output-dir` for another folder and `--limit-seconds 30` for a short trial. Existing outputs are protected unless `--force` is supplied. Run with `--help` for all options.

## Engineering focus

- Audio stream validation before model loading; explicit FFmpeg stream mapping to 16 kHz mono PCM.
- Vulkan runtime selection with no automatic CPU runtime fallback.
- Deterministic merging that retains overlapping speech from independent tracks.
- C# → Python JSON contract; speaker assignment by total temporal overlap, then nearest turn for gaps.
- Plain transcript recovery if diarization fails after transcription.
- Argument-list process invocation, concurrent stdout/stderr draining, isolated temporary directories, and overwrite checks.
- C# regression tests, real FFmpeg stream tests, Python alignment tests, and CI without model credentials.

See [architecture](docs/architecture.md), [setup and troubleshooting](docs/setup.md), and [validation](docs/validation.md).

## Scope and limitations

This is a small command-line tool, not a production transcription service or an accuracy benchmark. It loads a selected recording into the inference pipeline and processes tracks sequentially. Pyannote uses CPU and can take substantial time and RAM on long recordings. Whisper segments receive one speaker label each; a speaker change inside a segment is not split into word-level turns. Overlapping subtitles are retained and may be displayed differently by players.

Model downloads require network access. Audio processing is local; pyannote may contact Hugging Face to obtain model files. Cached models support offline use. Optional speaker assignment requires accepting the upstream model conditions and a token supplied through `HF_TOKEN`. No personal recordings, transcripts, credentials, model weights or Python environments are included here.

## License and attribution

My application code is available under the [MIT License](LICENSE). Whisper.net, whisper.cpp, Whisper, pyannote, PyTorch and FFmpeg are separate upstream projects with their own licenses and model terms. See [third-party notices](THIRD_PARTY_NOTICES.md) and [project provenance](docs/provenance.md).
