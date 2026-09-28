# Setup

## Core application

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Install [FFmpeg](https://ffmpeg.org/download.html), including `ffprobe`, and add its executable folder to PATH.
3. Install the current GPU driver for your Vulkan device and the [Visual C++ x64 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist).
4. From the repository root, run `dotnet restore --locked-mode` and `dotnet build -c Release --no-restore`.

Verify `dotnet --version`, `ffmpeg -version`, and `ffprobe -version`. Python is not required for plain or separate-track transcription. The Whisper.net native runtime requirements are documented [upstream](https://github.com/sandrohanea/whisper.net#runtimes-description).

The executable is `src/WhisperTranscriber/bin/Release/net10.0/WhisperTranscriber.exe`. You can also use `dotnet run --project src/WhisperTranscriber -c Release -- ...`.

## Optional speaker assignment

The tested environment uses Python 3.10 x64, CPU PyTorch 2.13.0 and pyannote.audio 4.0.7. Install Python 3.10 with the Windows `py` launcher, then:

```powershell
py -3.10 -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe -m pip install "torch==2.13.0+cpu" --index-url https://download.pytorch.org/whl/cpu
.\.venv\Scripts\python.exe -m pip install -r python/requirements-diarization.txt
.\.venv\Scripts\python.exe -m pip check
```

The requirements pin the main tested packages; the Python transitive dependency tree is not fully locked. A fresh environment install was not used for the publication smoke test; that test used an existing compatible environment.

Accept the conditions for [pyannote/speaker-diarization-community-1](https://huggingface.co/pyannote/speaker-diarization-community-1) with your Hugging Face account and create a read token. To pass it to the current PowerShell process without putting its literal value into command history:

```powershell
$credential = Get-Credential -UserName "huggingface" -Message "Enter the read token as password"
try {
    $env:HF_TOKEN = $credential.GetNetworkCredential().Password
    dotnet run --project src/WhisperTranscriber -c Release -- "C:\Media\conversation.wav" --single-track --speakers 2
} finally {
    Remove-Item Env:HF_TOKEN -ErrorAction SilentlyContinue
}
```

For an existing virtual environment, set `WHISPER_PYTHON` to its absolute `python.exe` path. The default is `.venv/Scripts/python.exe` in the repository root. The application sets `PYANNOTE_METRICS_ENABLED=0` for its Python child. Model access/download can still require network; for an already cached model set `HF_HUB_OFFLINE=1`.

## Paths and model selection

- `--model large`: cached `models/ggml-large-v3-turbo.bin`, downloaded if absent.
- `--model tiny`: cached `models/ggml-tiny.bin`, downloaded if absent.
- `--model-path`: explicit local GGML file; overrides the above, with no automatic download. Quantized or other models have not been tested here.
- `WHISPER_TRANSCRIBER_HOME`: optional existing application root containing `python/diarize.py`, `models/` and optional `.venv/`.
- Default output suffix: `_Transcript_AMD`, `_Transcript_AMD_Mehrspur_2Sprecher`, or `_Transcript_AMD_2Sprecher` / `_Transcript_AMD_3Sprecher`.
- TXT and SRT use UTF-8 with BOM. Successful diarization also writes `_Sprecher.json` with turns and labels.

Without an explicit home, a source checkout is found by `WhisperTranscriber.slnx`; a published executable falls back to its own directory. `dotnet publish src/WhisperTranscriber -c Release -o artifacts/app` includes the Python helper. Set up `.venv` there or use `WHISPER_PYTHON` if running that copy.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Fewer than three audio streams | Use `--plain` for an ordinary recording, or select existing streams with `--mic-track` and `--desktop-track`. Stereo channels alone do not satisfy this layout. |
| Vulkan/native library failure | Check the GPU driver, Visual C++ runtime and native Whisper.net prerequisites. No CPU runtime fallback is configured. |
| Token or gated-model error | Confirm read-token access and acceptance of the model conditions. Never commit the token. |
| `_OHNE_SPRECHER` output | Transcription succeeded, but diarization failed; the application returns a failure code and saves the plain result. |
| Existing output error | Choose another `--output-dir` or intentionally use `--force`. |
| Hallucinated text on silence or poor audio | Review the result; this tool has no separate VAD preprocessing or accuracy guarantee. |

Use one process per input/output location and model cache. Concurrent runs sharing filenames are not coordinated. Downloads use a temporary file, but are not checksum-pinned. A successful model load is not a transcription-quality guarantee.
