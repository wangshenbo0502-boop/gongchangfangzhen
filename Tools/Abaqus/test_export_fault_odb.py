"""Protocol tests with synthetic objects; no engineering solutions are generated."""
import math
import sys
import types
import unittest
from unittest.mock import patch

import export_fault_odb as exporter


class Field:
    validInvariants = ("MISES",)

    def __init__(self, values):
        self.values = [types.SimpleNamespace(mises=value, magnitude=value, data=value) for value in values]
        self.region = None

    def getSubset(self, region):
        self.region = region
        return self


def frame(time, **fields):
    return types.SimpleNamespace(frameValue=time, fieldOutputs=fields)


class ExportTests(unittest.TestCase):
    def test_units_instance_selection_and_independent_envelopes(self):
        stress, displacement, temperature = Field([1e6, 3e6]), Field([.001, .002]), Field([290, 300])
        step = types.SimpleNamespace(frames=[
            frame(0), frame(1, S=stress, U=displacement, NT11=temperature)
        ])
        region = object()
        with patch.dict(sys.modules, {"abaqusConstants": types.SimpleNamespace(MISES="MISES")}):
            frames, skipped = exporter.export_frames(step, region, ["S", "U", "NT11"], 1e-6, 1000, -273.15)
        self.assertEqual(skipped, 1)
        self.assertEqual(frames[0]["maxStressMPa"], 3)
        self.assertEqual(frames[0]["maxDisplacementMm"], 2)
        self.assertAlmostEqual(frames[0]["maxTemperatureC"], 26.85)
        self.assertIs(stress.region, region)
        self.assertIs(displacement.region, region)
        self.assertIs(temperature.region, region)

    def test_missing_channels_are_not_filled_with_fake_zero(self):
        step = types.SimpleNamespace(frames=[frame(0, U=Field([1]))])
        with self.assertRaisesRegex(ValueError, "No frames"):
            exporter.export_frames(step, object(), ["S", "U"], 1, 1, 0)

    def test_temperature_only(self):
        step = types.SimpleNamespace(frames=[frame(0, TEMP=Field([40, 60]))])
        frames, _ = exporter.export_frames(step, object(), ["TEMP"], 1, 1, 0)
        self.assertEqual(frames, [{"time": 0, "maxTemperatureC": 60}])

    def test_duplicate_time_is_rejected(self):
        step = types.SimpleNamespace(frames=[frame(1, U=Field([1])), frame(1, U=Field([2]))])
        with self.assertRaisesRegex(ValueError, "strictly increasing"):
            exporter.export_frames(step, object(), ["U"], 1, 1, 0)

    def test_nonfinite_field_and_empty_instance_are_rejected(self):
        for values in ([math.inf], [math.nan], []):
            with self.subTest(values=values), self.assertRaises(ValueError):
                exporter.export_frames(types.SimpleNamespace(frames=[frame(0, U=Field(values))]),
                                       object(), ["U"], 1, 1, 0)

    def test_missing_mises_invariant_is_rejected(self):
        stress = Field([1])
        stress.validInvariants = ()
        with patch.dict(sys.modules, {"abaqusConstants": types.SimpleNamespace(MISES="MISES")}):
            with self.assertRaisesRegex(ValueError, "Mises"):
                exporter.export_frames(types.SimpleNamespace(frames=[frame(0, S=stress)]),
                                       object(), ["S"], 1, 1, 0)


if __name__ == "__main__":
    unittest.main()
