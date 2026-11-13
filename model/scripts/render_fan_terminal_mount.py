"""Render the revised mount closely and refresh the four whole-machine views."""
import sys
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_fan

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.cycles.samples = 64
for filename, position, target, scale, fit in (
    ("terminal-mount-clearance.png", (-1.1, -2.4, 2.45), (.15, -.56, 1.59), .82, False),
    ("terminal-mount-fit.png", (1.5, -1.9, 2.55), (.16, -.53, 1.58), .82, False),
    ("assembled.png", (-3.7, -4.5, 2.95), (0, -.025, .90), 4, True),
    ("inlet-front.png", (-5, -.08, 1.27), (0, 0, .90), 4, True),
    ("motor-side.png", (3.8, -4.5, 2.70), (0, -.025, .90), 4, True),
    ("elevated.png", (-3.6, -4.2, 4.55), (0, 0, .90), 4, True),
):
    build_fan.camera(scene, position, target, scale)
    build_fan.render(scene, filename, fit=fit)
