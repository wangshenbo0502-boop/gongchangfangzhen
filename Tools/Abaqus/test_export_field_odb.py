"""Synthetic API contracts only. These tests do not create or verify an ODB."""
import types
import unittest

from export_field_odb import export_mesh, export_frame, cross, subtract, dot


def obj(**kwargs):
    return types.SimpleNamespace(**kwargs)


def instance():
    # Coordinates already contain a 90 degree rotation and translation.
    return obj(name="TERMINAL-1",
               nodes=[obj(label=n, coordinates=p) for n, p in (
                   (70, (10, 20, 30)), (4, (10, 21, 30)),
                   (900, (9, 20, 30)), (23, (10, 20, 31)), (51, (10, 20, 29)))],
               elements=[obj(label=403, type="DC3D4", connectivity=(70, 4, 900, 23)),
                         obj(label=9, type="DC3D4", connectivity=(70, 900, 4, 51))])


class FieldExportTests(unittest.TestCase):
    def test_noncontiguous_labels_and_boundary(self):
        mesh = export_mesh(instance(), .001)
        self.assertEqual(mesh["nodeLabels"], [4, 23, 51, 70, 900])
        self.assertEqual(len(mesh["triangles"]), 18)
        self.assertEqual(set(mesh["triangleElementLabels"]), {403, 9})
        point = lambda i: mesh["coordinatesM"][i*3:i*3+3]
        # Global rotated/translated point is scaled exactly once, not repositioned.
        self.assertEqual(point(0), [.01, .021, .03])
        self.assertTrue(all(1 <= face <= 4 for face in mesh["triangleFaces"]))
        for face in range(6):
            tri = mesh["triangles"][face*3:face*3+3]
            element = next(e for e in mesh["elements"] if e["elementLabel"] == mesh["triangleElementLabels"][face])
            opposite = next(n for n in element["nodeIndices"] if n not in tri)
            normal = cross(subtract(point(tri[1]), point(tri[0])), subtract(point(tri[2]), point(tri[0])))
            self.assertLess(dot(normal, subtract(point(opposite), point(tri[0]))), 0)

    def test_temperature_maps_by_label_not_order(self):
        part = instance()
        values = [obj(position="NODAL", instance=part, nodeLabel=n.label, data=n.label + 273.15)
                  for n in reversed(part.nodes)]
        field = obj(getSubset=lambda **_: obj(values=values))
        frame = obj(frameValue=2.7, fieldOutputs={"NT11": field})
        result = export_frame(frame, 3, part, [4, 23, 51, 70, 900], -273.15, "NODAL")
        for value, expected in zip(result["temperatureC"], [4, 23, 51, 70, 900]):
            self.assertAlmostEqual(value, expected)
        self.assertEqual(result["axisValue"], 2.7)
        values.pop()
        with self.assertRaisesRegex(ValueError, "cover"):
            export_frame(frame, 3, part, [4, 23, 51, 70, 900], -273.15, "NODAL")

    def test_unsupported_quadratic_element_is_rejected(self):
        part = instance()
        part.elements[0].type = "DC3D10"
        with self.assertRaisesRegex(ValueError, "DC3D10"):
            export_mesh(part, 1)

    def test_degenerate_duplicate_and_nonfinite(self):
        part = instance()
        part.nodes[0].coordinates = (float("nan"), 0, 0)
        with self.assertRaises(ValueError):
            export_mesh(part, 1)
        part = instance()
        part.nodes[0].label = 4
        with self.assertRaises(ValueError):
            export_mesh(part, 1)
        part = instance()
        part.elements[0].connectivity = (70, 70, 900, 23)
        with self.assertRaises(ValueError):
            export_mesh(part, 1)

    def test_missing_or_wrong_position_field(self):
        part = instance()
        frame = obj(frameValue=0, fieldOutputs={})
        with self.assertRaisesRegex(ValueError, "Missing"):
            export_frame(frame, 0, part, [4], 0, "NODAL")
        frame.fieldOutputs["NT11"] = obj(getSubset=lambda **_: obj(values=[
            obj(position="INTEGRATION_POINT", instance=part, nodeLabel=4, data=25)]))
        with self.assertRaisesRegex(ValueError, "NODAL"):
            export_frame(frame, 0, part, [4], 0, "NODAL")


if __name__ == "__main__":
    unittest.main()
