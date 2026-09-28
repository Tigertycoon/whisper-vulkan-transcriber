"""Ordnet Whisper-Segmente lokal einer festen Zahl von Sprechern zu."""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--audio", required=True)
    parser.add_argument("--segments", required=True)
    parser.add_argument("--labeled-segments", required=True)
    parser.add_argument("--speaker-json", required=True)
    parser.add_argument("--num-speakers", required=True, type=int, choices=(2, 3))
    parser.add_argument("--speaker-prefix", required=True)
    return parser.parse_args()


def load_token() -> str:
    token = os.environ.get("HF_TOKEN", "").strip()
    if not token:
        raise RuntimeError("Set HF_TOKEN in the current process for optional diarization.")
    return token


def run_diarization(
    audio_path: Path,
    token: str,
    number_of_speakers: int,
) -> list[dict[str, Any]]:
    import numpy as np
    import torch
    from pyannote.audio import Pipeline
    from pyannote.audio.pipelines.utils.hook import ProgressHook
    from scipy.io import wavfile

    thread_count = max(1, (os.cpu_count() or 2) - 1)
    torch.set_num_threads(thread_count)
    print(f"pyannote nutzt CPU mit bis zu {thread_count} Threads.", flush=True)

    pipeline = Pipeline.from_pretrained(
        "pyannote/speaker-diarization-community-1",
        token=token,
    )
    pipeline.to(torch.device("cpu"))

    print("Lade das vorbereitete WAV direkt (TorchCodec wird umgangen) ...", flush=True)
    sample_rate, pcm = wavfile.read(audio_path, mmap=True)
    if sample_rate != 16000:
        raise RuntimeError(f"Erwartet wurden 16000 Hz, erhalten wurden {sample_rate} Hz.")
    if pcm.ndim != 1:
        raise RuntimeError(f"Erwartet wurde Mono-WAV, erhalten wurde Shape {pcm.shape}.")
    if pcm.dtype != np.int16:
        raise RuntimeError(f"Erwartet wurde PCM int16, erhalten wurde {pcm.dtype}.")

    duration_seconds = len(pcm) / sample_rate
    waveform_np = pcm.astype(np.float32)
    waveform_np *= 1.0 / 32768.0
    waveform = torch.from_numpy(waveform_np).unsqueeze(0)
    del pcm, waveform_np
    audio = {
        "waveform": waveform,
        "sample_rate": sample_rate,
        "uri": audio_path.stem,
    }
    print(
        f"WAV geladen: {duration_seconds / 60.0:.1f} Minuten, "
        f"{waveform.numel() * waveform.element_size() / (1024 ** 3):.2f} GiB RAM.",
        flush=True,
    )

    with ProgressHook() as hook:
        output = pipeline(audio, num_speakers=number_of_speakers, hook=hook)

    annotation = getattr(output, "exclusive_speaker_diarization", output)
    turns: list[dict[str, Any]] = []
    for turn, _, speaker in annotation.itertracks(yield_label=True):
        turns.append(
            {
                "start": round(float(turn.start), 3),
                "end": round(float(turn.end), 3),
                "speaker": str(speaker),
            }
        )
    if not turns:
        raise RuntimeError("pyannote hat keine Sprecher-Zeitbereiche erzeugt.")
    return turns


def segment_value(segment: dict[str, Any], name: str) -> Any:
    for key, value in segment.items():
        if key.casefold() == name.casefold():
            return value
    raise KeyError(f"Segmentfeld fehlt: {name}")


def speaker_for_segment(
    segment: dict[str, Any],
    turns: list[dict[str, Any]],
) -> str:
    if not turns:
        raise ValueError("No speaker turns available.")
    start = float(segment_value(segment, "Start"))
    end = float(segment_value(segment, "End"))

    overlap_by_speaker: dict[str, float] = {}
    for turn in turns:
        speaker = str(turn["speaker"])
        overlap = max(0.0, min(end, turn["end"]) - max(start, turn["start"]))
        overlap_by_speaker[speaker] = overlap_by_speaker.get(speaker, 0.0) + overlap
    best_speaker = max(overlap_by_speaker, key=overlap_by_speaker.get)
    if overlap_by_speaker[best_speaker] > 0.0:
        return best_speaker

    midpoint = (start + end) / 2.0
    nearest = min(
        turns,
        key=lambda turn: 0.0
        if turn["start"] <= midpoint <= turn["end"]
        else min(abs(midpoint - turn["start"]), abs(midpoint - turn["end"])),
    )
    return str(nearest["speaker"])


def stable_speaker_names(
    turns: list[dict[str, Any]],
    prefix: str,
) -> dict[str, str]:
    first_seen: dict[str, float] = {}
    for turn in turns:
        speaker = str(turn["speaker"])
        first_seen[speaker] = min(first_seen.get(speaker, float("inf")), float(turn["start"]))
    ordered = sorted(first_seen, key=first_seen.get)
    return {speaker: f"{prefix} {index + 1}" for index, speaker in enumerate(ordered)}


def label_segments(
    segments: list[dict[str, Any]],
    turns: list[dict[str, Any]],
    prefix: str,
) -> list[dict[str, Any]]:
    names = stable_speaker_names(turns, prefix)
    labeled: list[dict[str, Any]] = []
    for segment in segments:
        raw_speaker = speaker_for_segment(segment, turns)
        labeled.append(
            {
                "Start": float(segment_value(segment, "Start")),
                "End": float(segment_value(segment, "End")),
                "Text": str(segment_value(segment, "Text")).strip(),
                "Speaker": names.get(raw_speaker, f"{prefix} ?"),
                "RawSpeaker": raw_speaker,
            }
        )
    return labeled


def main() -> int:
    args = parse_args()
    token = load_token()
    segments = json.loads(Path(args.segments).read_text(encoding="utf-8-sig"))
    turns = run_diarization(Path(args.audio), token, args.num_speakers)
    labeled = label_segments(segments, turns, args.speaker_prefix)

    Path(args.labeled_segments).write_text(
        json.dumps(labeled, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    speaker_payload = {
        "requested_speakers": args.num_speakers,
        "speaker_prefix": args.speaker_prefix,
        "turns": turns,
        "segments": labeled,
    }
    Path(args.speaker_json).write_text(
        json.dumps(speaker_payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(
        f"Sprecher-Zuordnung fertig: {len(labeled)} Textsegmente, "
        f"genau {args.num_speakers} Sprecher angefordert.",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"FEHLER Sprecher-Erkennung: {error}", file=sys.stderr, flush=True)
        raise
