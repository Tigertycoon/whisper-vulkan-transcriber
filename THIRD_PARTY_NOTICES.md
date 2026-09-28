# Third-party projects

The MIT license in this repository covers the application code. It does not relicense dependencies, pretrained models or external executables. This source repository does not vendor their implementations, model weights or binaries.

| Project | Role | Upstream |
| --- | --- | --- |
| Whisper.net | .NET binding, model downloader and Vulkan runtime package | [repository and MIT license](https://github.com/sandrohanea/whisper.net) |
| whisper.cpp | Native Whisper inference used by Whisper.net | [repository and MIT license](https://github.com/ggml-org/whisper.cpp) |
| OpenAI Whisper | Pretrained speech recognition models | [repository and MIT license](https://github.com/openai/whisper) |
| pyannote.audio | Optional Python diarization framework | [repository and MIT license](https://github.com/pyannote/pyannote-audio) |
| pyannote speaker-diarization-community-1 | Optional pretrained diarization pipeline | [model card, conditions and license](https://huggingface.co/pyannote/speaker-diarization-community-1) |
| PyTorch, NumPy, SciPy | Optional Python inference/numerics | [PyTorch](https://github.com/pytorch/pytorch), [NumPy](https://numpy.org/doc/stable/license.html), [SciPy](https://github.com/scipy/scipy/blob/main/LICENSE.txt) |
| FFmpeg / ffprobe | External media conversion and stream inspection | [licensing](https://ffmpeg.org/legal.html); terms depend on the installed build |
| xUnit and Microsoft.NET.Test.Sdk | Development and test dependencies | [xUnit](https://github.com/xunit/xunit), [VSTest](https://github.com/microsoft/vstest) |

Install dependencies and obtain models from their official sources. Any redistributed compiled bundle must include the applicable third-party license notices. This repository publishes source, not such a bundle.
