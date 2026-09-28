# Validation

Publication checks performed on **2026-09-28**. These are functional integration checks, not transcription accuracy or diarization-quality benchmarks.

| Check | Result |
| --- | --- |
| .NET 10 Release build with warnings as errors | Passed; 0 warnings, 0 errors |
| C# tests | 17 passed, including real FFmpeg extraction and frequency checks for selected streams |
| Python alignment tests | 8 passed without loading models or requiring credentials |
| Plain transcription on RX 6950 XT | Passed using the existing multilingual tiny GGML model and synthetic speech |
| Two-stream Vulkan transcription | Passed; native log selected `Vulkan0`; TXT/SRT contained microphone and desktop labels |
| Optional two-speaker pipeline | Passed offline with cached pyannote model and existing Python environment; 6 turns, 3 transcript segments, 2 output speaker labels |
| Diarization failure recovery | Passed with a deliberately failing Python helper: exit 1 plus rescued plain TXT/SRT |
| NuGet vulnerability query, including transitive dependencies | No vulnerable packages reported by configured sources on the validation date |

The GPU fixtures were generated locally through Windows speech synthesis. No personal conversation was used or published. Recognition text was allowed to vary; assertions covered output existence, timing, labels, stream mapping and actual backend selection.

## Run the automated tests

Install .NET 10, FFmpeg/ffprobe and Python 3.10 or newer, then from the repository root:

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore --warnaserror
dotnet test -c Release --no-build
python -m unittest discover -s tests -p 'test_*.py' -v
```

[GitHub Actions](https://github.com/Tigertycoon/whisper-vulkan-transcriber/actions/workflows/ci.yml) runs these checks on Windows. CI does not perform GPU inference, install pyannote, use a token or download speech models.

## Reproduce the local Vulkan smoke test

Requires a compatible GPU, existing GGML model, FFmpeg and at least one installed Windows desktop speech synthesis voice. It creates a new directory under ignored `artifacts/` on each run and leaves logs/results there for inspection:

```powershell
.\scripts\smoke-vulkan.ps1 -ModelPath "C:\Models\ggml-tiny.bin"
```

This runs real native inference. It checks for an active Vulkan backend and both source labels in SRT; it does not assert that every synthesized word is recognized correctly.

## Remaining limits

- The new public source was tested with `tiny`; no fresh end-to-end run with `large-v3-turbo` was performed for this publication.
- The two-speaker pyannote path used a previously installed compatible environment and cached model, not a fresh dependency/model install.
- The three-speaker CLI configuration is covered by tests, but a three-speaker model run was not repeated for this publication.
- Other GPUs, non-Windows systems, long-recording performance, accuracy across languages, silence handling and subtitle-player overlap rendering have not been comprehensively validated.
- Drag-and-drop wrappers build and call the same CLI; interactive drag-and-drop was not separately exercised.
