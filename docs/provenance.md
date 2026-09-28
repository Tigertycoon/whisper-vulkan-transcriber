# Project provenance

This repository curates my local transcription tools into one distributable source project. The original tools grew from a small faster-whisper script into a C# Vulkan workflow for an AMD workstation, followed by separate-track and mixed-audio speaker variants.

The public application is based primarily on the more complete multitrack C# version. Publication cleanup restored plain transcription as an explicit mode, removed dependencies on a neighboring private folder, introduced configurable paths/language, separated source responsibilities, added output protection and corrected process-output draining and temporal speaker aggregation. Tests, setup documentation and CI were added for this publication.

The older CUDA/CPU scripts, duplicate application variants, generated executables, model weights, environments, credentials and personal media/transcripts are intentionally absent. The original local folders were preserved. The Git history here begins at this curated publication; it does not pretend to reproduce the earlier development history.

My contribution is the application workflow, audio-stream handling, process integration, output formats, speaker alignment and usability. The speech recognition and diarization models and runtimes are upstream work, listed in [third-party notices](../THIRD_PARTY_NOTICES.md).
