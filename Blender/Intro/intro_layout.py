"""Shared layout of the intro cutscene, in Unity museum coordinates (metres, X east, Y up, Z north).

Blender is right-handed and Z up. The FBX export used by this project maps Blender (x, y, z) to
Unity (-x, z, -y), so `unity` converts a museum position to the Blender position that lands on it.
"""
from math import cos, exp, radians, sin
from mathutils import Vector

FPS = 30
LENGTH = 28.6                      # seconds
WALL_Z = 30.35                     # north edge of the roof overhang
ROOF_Y = 3.81
EDGE_Z = 21.65                     # courtyard edge of the roof
SPAWN = (1.6, 0.0, 18.75)          # behind the flower pot, facing the glass door

VAN_STOP = (1.0, 0.0, 35.2)        # van centre; it heads towards -X, so its left side faces the museum
VAN_FLOOR = 0.52
VAN_SIDE_Z = 34.25
DOOR_X = 1.35                      # middle of the sliding door opening

LADDER_BASE = (0.0, 0.0, 31.7)
LADDER_LENGTH = 4.95
RUNG = 0.30                        # spacing along the ladder
LADDER_REST = -19.2                # degrees from vertical, leaning on the roof edge
LADDER_START = 28.0                # tipped back towards the thief


def unity(x, y, z):
    return Vector((-x, -z, y))


def ladder_angle(t):
    """Degrees from vertical; positive is away from the wall."""
    start, end = 9.4, 11.2
    u = max(0.0, min(1.0, (t - start) / (end - start)))
    angle = LADDER_START + (LADDER_REST - LADDER_START) * u * u * (1.6 - 0.6 * u)
    if t > end:                    # a small rattle when it hits the roof
        angle += 1.1 * abs(sin((t - end) * 22.0)) * exp(-(t - end) * 9.0)
    return angle


def ladder_point(s, t, side=0.0, out=0.0):
    """Point s metres up the ladder, `side` metres east and `out` metres off its climbing face."""
    a = radians(ladder_angle(t))
    x, y, z = LADDER_BASE
    return (x + side, y + s * cos(a) - out * sin(a), z + s * sin(a) + out * cos(a))
