import importlib.util
import os
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("diarize", Path(__file__).parents[1] / "python/diarize.py")
diarize = importlib.util.module_from_spec(spec)
spec.loader.exec_module(diarize)


class SpeakerAssignmentTests(unittest.TestCase):
    def test_total_overlap_wins_over_longest_individual_turn(self):
        turns = [dict(start=0, end=2, speaker="a"), dict(start=2, end=5, speaker="b"),
                 dict(start=5, end=7, speaker="a")]
        self.assertEqual("a", diarize.speaker_for_segment(dict(Start=0, End=7), turns))

    def test_gap_uses_nearest_turn_to_segment_midpoint(self):
        turns = [dict(start=0, end=1, speaker="a"), dict(start=4, end=6, speaker="b")]
        self.assertEqual("b", diarize.speaker_for_segment(dict(Start=3, End=3.5), turns))

    def test_names_follow_first_appearance_even_with_unsorted_turns(self):
        turns = [dict(start=9, end=10, speaker="b"), dict(start=2, end=3, speaker="a"),
                 dict(start=0, end=1, speaker="b")]
        self.assertEqual({"b": "Sprecher 1", "a": "Sprecher 2"}, diarize.stable_speaker_names(turns, "Sprecher"))

    def test_csharp_json_contract_and_unicode_survive_labeling(self):
        result = diarize.label_segments([dict(Start=1, End=2, Text=" Grüße ", Speaker="")],
                                       [dict(start=0, end=3, speaker="raw")], "Sprecher")
        self.assertEqual(dict(Start=1.0, End=2.0, Text="Grüße", Speaker="Sprecher 1", RawSpeaker="raw"), result[0])

    def test_json_fields_are_case_insensitive(self):
        self.assertEqual(1, diarize.segment_value({"START": 1}, "Start"))

    def test_empty_turns_have_clear_error(self):
        with self.assertRaisesRegex(ValueError, "No speaker turns"):
            diarize.speaker_for_segment(dict(Start=0, End=1), [])

    def test_missing_token_errors_without_local_file_lookup(self):
        with patch.dict(os.environ, {}, clear=True):
            with self.assertRaisesRegex(RuntimeError, "HF_TOKEN"):
                diarize.load_token()

    def test_environment_token_is_trimmed(self):
        with patch.dict(os.environ, {"HF_TOKEN": " placeholder-only "}):
            self.assertEqual("placeholder-only", diarize.load_token())


if __name__ == "__main__":
    unittest.main()
