"""Animates the thief for the intro cutscene and exports the FBX. Unity game.

Run after build_thief.py:
    blender --background Blender/Intro/MuseumThief.blend --python Blender/Intro/animate_thief.py

The performance is described by contacts: each foot and hand has a list of timed moves between points in the
museum, and the hips follow keyed positions. Two-bone IK then bakes every frame, so feet stay planted, hands
hold the ladder rungs and the landing touches the real floor.
"""
import bpy
import json
import os
import sys
from bisect import bisect_right
from math import acos, cos, pi, radians, sin, sqrt
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from intro_layout import *  # noqa: E402

ACTION = "Thief_Intro"
ANKLE = 0.09
PALM = 0.10                        # wrist to the middle of the closed hand
events = []                        # (time, kind) for the sound track


def smooth(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


class Track:
    """Keyed numbers, interpolated with a monotone cubic so nothing overshoots."""
    def __init__(self, *keys):
        self.keys = sorted(keys)

    def add(self, time, value):
        self.keys = sorted([k for k in self.keys if abs(k[0] - time) > 1e-4] + [(time, value)])

    def __call__(self, t):
        k = self.keys
        if t <= k[0][0]: return k[0][1]
        if t >= k[-1][0]: return k[-1][1]
        i = bisect_right([a for a, _ in k], t) - 1
        (t0, v0), (t1, v1) = k[i], k[i + 1]
        h = t1 - t0
        d = (v1 - v0) / h

        def slope(j):
            if j == 0 or j == len(k) - 1: return 0.0
            a = (k[j][1] - k[j - 1][1]) / (k[j][0] - k[j - 1][0])
            b = (k[j + 1][1] - k[j][1]) / (k[j + 1][0] - k[j][0])
            return 0.0 if a * b <= 0 else 2 * a * b / (a + b)
        m0, m1 = slope(i), slope(i + 1)
        u = (t - t0) / h
        return v0 + h * u * (m0 + u * ((3 * d - 2 * m0 - m1) + u * (m0 + m1 - 2 * d)))


class Limb:
    """Timed moves of a hand or foot. Targets may be points or functions of time."""
    def __init__(self, start):
        self.start = start
        self.moves = []

    def move(self, t0, t1, target, lift=0.0, sound=None):
        self.moves.append((t0, t1, target, lift))
        if sound: events.append((t1, sound))

    @staticmethod
    def at(target, t):
        return Vector(target(t)) if callable(target) else Vector(target)

    def __call__(self, t):
        previous = self.start
        for t0, t1, target, lift in self.moves:
            if t < t0: break
            if t < t1:
                u = smooth((t - t0) / (t1 - t0))
                point = self.at(previous, t).lerp(self.at(target, t), u)
                point.y += lift * sin(pi * (t - t0) / (t1 - t0))
                return point
            previous = target
        return self.at(previous, t)


class Scene:
    """One continuous piece of performance. Positions are Unity museum coordinates."""
    def __init__(self, t0, t1, hips, yaw):
        self.t0, self.t1 = t0, t1
        self.hx, self.hy, self.hz = (Track((t0, v)) for v in hips)
        self.yaw = Track((t0, yaw))
        self.hip_pitch, self.hip_roll = Track((t0, 0)), Track((t0, 0))
        self.bend, self.twist = Track((t0, 4)), Track((t0, 0))
        self.look, self.turn = Track((t0, 0)), Track((t0, 0))
        self.foot_pitch = {"L": Track((t0, 0)), "R": Track((t0, 0))}
        self.foot_yaw = {"L": Track((t0, yaw)), "R": Track((t0, yaw))}
        self.swing = Track((t0, 1))           # how far relaxed arms swing
        self.feet = {}
        self.hands = {}                        # only while holding something
        self.hold = {"L": Track((t0, 0)), "R": Track((t0, 0))}
        self.phase = []                        # (time, +1 left foot forward / -1 right)

    def hips(self, t, x=None, y=None, z=None):
        for track, value in ((self.hx, x), (self.hy, y), (self.hz, z)):
            if value is not None: track.add(t, value)

    def spot(self, x, z, yaw, side, y=0.0, ahead=0.0, width=0.10):
        a = radians(yaw)
        forward, right = Vector((sin(a), 0, cos(a))), Vector((cos(a), 0, -sin(a)))
        s = width if side == "R" else -width
        return Vector((x, y, z)) + right * s + forward * ahead

    def walk(self, t0, t1, start, end, steps, height, y=0.0, first="L", lift=0.10, bob=0.025, sound="step", yaw=None, width=0.10):
        """Alternating steps from one (x, z) to another, ending with the feet together."""
        yaw = self.yaw(t0) if yaw is None else yaw
        step_time = (t1 - t0) / (steps + 1)
        start, end = Vector(start), Vector(end)
        side = first
        for i in range(steps + 1):
            reach = min(1.0, (i + 0.95) / steps) if i < steps - 1 else 1.0
            point = start.lerp(end, reach)
            begin = t0 + i * step_time
            self.feet[side].move(begin + 0.08 * step_time, begin + step_time, self.spot(point.x, point.y, yaw, side, y, 0.02, width), lift, sound)
            middle = begin + 0.5 * step_time
            for dt, pitch in ((0.0, 0), (0.15, 24), (0.5, 8), (0.86, -14), (1.0, 0)):
                self.foot_pitch[side].add(begin + dt * step_time, pitch)
            self.foot_yaw[side].add(begin, self.foot_yaw[side](begin))
            self.foot_yaw[side].add(begin + step_time, yaw)
            self.phase.append((begin + 0.5 * step_time, 0))
            self.phase.append((begin + step_time, 1 if side == "L" else -1))
            # Hips stay midway between the two planted feet, highest while one leg passes the other.
            at = start.lerp(end, min(1.0, i / steps))
            self.hips(middle, at.x, y + height + bob, at.y)
            at = start.lerp(end, min(1.0, (i + 0.5) / steps))
            self.hips(begin + step_time, at.x, y + height - bob, at.y)
            side = "R" if side == "L" else "L"
        self.hips(t1 + 0.12, end.x, y + height, end.y)
        self.phase.append((t1 + 0.25, 0))

    def swing_amount(self, t):
        if not self.phase or t < self.phase[0][0] - 0.2 or t > self.phase[-1][0]: return 0.0
        previous = (self.phase[0][0] - 0.2, 0)
        for key in self.phase:
            if t <= key[0]:
                u = smooth((t - previous[0]) / max(1e-4, key[0] - previous[0]))
                return previous[1] + (key[1] - previous[1]) * u
            previous = key
        return 0.0


# ------------------------------------------------------------------------------------------ performance

def leave_the_van():
    s = Scene(0.0, 9.0, (DOOR_X, VAN_FLOOR + 0.55, 34.72), 180)
    floor = VAN_FLOOR
    s.feet["L"] = Limb((DOOR_X + 0.12, floor, 34.62))
    s.feet["R"] = Limb((DOOR_X - 0.12, floor, 34.62))
    frame = (DOOR_X + 0.62, 1.30, VAN_SIDE_Z - 0.01)
    s.hands["L"], s.hands["R"] = Limb(frame), Limb((DOOR_X - 0.30, 0.95, 34.3))
    s.bend.add(5.5, 38); s.bend.add(6.2, 42); s.bend.add(7.1, 6); s.bend.add(9.0, 8)
    s.bend.add(0.0, 38)
    s.look.add(5.4, -22); s.look.add(6.2, 12); s.look.add(7.0, 0); s.look.add(0.0, -22)
    s.hold["L"].add(5.2, 0); s.hold["L"].add(5.55, 1); s.hold["L"].add(6.05, 1); s.hold["L"].add(6.5, 0)
    # One leg down, then the other.
    s.feet["R"].move(5.75, 6.30, (DOOR_X - 0.10, 0, 33.92), 0.12, "step")
    s.feet["L"].move(6.30, 6.90, (DOOR_X + 0.10, 0, 33.62), 0.16, "step")
    s.hips(5.5, DOOR_X, floor + 0.55, 34.70)
    s.hips(5.9, DOOR_X, floor + 0.50, 34.48)
    s.hips(6.3, DOOR_X - 0.04, 0.76, 34.05)
    s.hips(6.6, DOOR_X - 0.04, 0.78, 33.84)
    s.hips(7.0, DOOR_X, 0.885, 33.66)
    s.feet["R"].move(6.95, 7.25, (DOOR_X - 0.10, 0, 33.62), 0.06)
    s.foot_pitch["R"].add(5.75, 0); s.foot_pitch["R"].add(6.1, 26); s.foot_pitch["R"].add(6.3, 0)
    s.foot_pitch["L"].add(6.30, 0); s.foot_pitch["L"].add(6.5, 30); s.foot_pitch["L"].add(6.9, 0)
    # A careful look both ways.
    s.turn.add(7.0, 0); s.turn.add(7.35, 58); s.turn.add(7.65, 58); s.turn.add(8.1, -60); s.turn.add(8.4, -60); s.turn.add(8.75, 0)
    s.twist.add(7.0, 0); s.twist.add(7.4, 16); s.twist.add(8.1, -16); s.twist.add(8.75, 0)
    s.hips(8.3, DOOR_X, 0.875, 33.66)
    s.walk(8.35, 9.0, (DOOR_X, 33.66), (DOOR_X - 0.35, 33.10), 1, 0.87, first="L", bob=0.01, yaw=190)
    return s


def rung(number, side):
    s = 0.17 if side == "L" else -0.17     # facing -Z, the left hand is at +X
    return lambda t: ladder_point(number * RUNG, t, s, 0.035)


def step_on(number, side):
    s = 0.10 if side == "L" else -0.10
    return lambda t: Vector(ladder_point(number * RUNG, t, s, 0.07)) + Vector((0, 0.028, 0))


def ladder_roof_and_jump():
    s = Scene(9.0, LENGTH, (0, 0.85, 33.02), 180)
    s.feet["L"], s.feet["R"] = Limb(s.spot(0, 33.0, 180, "L", ahead=0.2)), Limb(s.spot(0, 33.0, 180, "R", ahead=-0.15))
    s.hands["L"], s.hands["R"] = Limb(rung(5, "L")), Limb(rung(4, "R"))
    for side in "LR": s.hold[side].add(9.0, 1)
    # Push the ladder up against the roof.
    s.bend.add(9.0, 22); s.bend.add(10.2, 24); s.bend.add(11.1, 10); s.bend.add(11.6, 3)
    s.look.add(9.0, -14); s.look.add(10.5, -30); s.look.add(11.5, -38); s.look.add(12.2, -22)
    s.walk(9.3, 11.0, (0, 33.0), (0, 31.74), 3, 0.83, first="R", yaw=180)
    events.append((11.2, "ladder"))
    s.hips(11.6, 0, 0.86, 31.72)
    # Climb: opposite hand and foot together, one half-step every 0.4 s.
    half, begin = 0.40, 11.9
    for j in range(12):
        t0 = begin + j * half
        foot, hand = ("L", "R") if j % 2 == 0 else ("R", "L")
        s.feet[foot].move(t0, t0 + 0.30, step_on(j + 1, foot), 0.05, "rung")
        s.hands[hand].move(t0 + 0.03, t0 + 0.30, rung(j + 5, hand), 0.03)
        s.foot_pitch[foot].add(t0, s.foot_pitch[foot](t0)); s.foot_pitch[foot].add(t0 + 0.15, 22); s.foot_pitch[foot].add(t0 + 0.30, -8)
        hx, hy, hz = ladder_point((j + 0.75) * RUNG + 0.62, 20.0, 0.05 if foot == "R" else -0.05, 0.36)
        s.hips(t0 + 0.34, hx, max(hy, 0.86), hz)
        s.hip_roll.add(t0 + 0.15, 5 if foot == "L" else -5)
    top = begin + 12 * half
    s.hip_roll.add(top + 0.4, 0)
    s.bend.add(12.2, 12); s.bend.add(top - 0.3, 14); s.bend.add(top + 0.5, 34); s.bend.add(top + 1.2, 30)
    s.look.add(top - 0.5, -24); s.look.add(top + 0.8, -18)
    # Step off sideways, round the top of the ladder.
    s.feet["L"].move(top + 0.05, top + 0.60, (0.52, ROOF_Y, 29.84), 0.22, "roof")
    s.hips(top + 0.20, 0.12, 4.16, 30.56)
    s.hips(top + 0.55, 0.30, 4.30, 30.26)
    s.feet["R"].move(top + 0.50, top + 1.15, (0.34, ROOF_Y, 29.52), 0.30, "roof")
    s.hips(top + 0.95, 0.44, 4.48, 29.82)
    s.feet["L"].move(top + 1.25, top + 1.55, (0.56, ROOF_Y, 29.50), 0.06)
    s.hold["L"].add(top - 0.05, 1); s.hold["L"].add(top + 0.35, 0.0)
    s.hold["R"].add(top + 0.25, 1); s.hold["R"].add(top + 0.75, 0.0)
    for side in "LR": s.foot_pitch[side].add(top + 0.6, 0); s.foot_pitch[side].add(top + 1.3, 0)
    # A low, quick sneak over the roof.
    sneak = top + 1.7                        # about 18.4
    s.hips(sneak, 0.46, ROOF_Y + 0.66, 29.52)
    s.bend.add(sneak, 40); s.look.add(sneak, -30)
    heading = 180 - 6.2
    s.yaw.add(sneak - 0.1, 180); s.yaw.add(sneak + 0.4, heading)
    s.walk(sneak, 22.0, (0.46, 29.5), (1.3, 22.02), 11, 0.66, y=ROOF_Y, first="R", lift=0.14, bob=0.03, sound="roof", yaw=heading, width=0.12)
    s.turn.add(19.6, 0); s.turn.add(20.0, 35); s.turn.add(20.5, 35); s.turn.add(21.0, 0)
    s.bend.add(21.6, 40)
    # Crouch at the edge and look down into the garden.
    s.yaw.add(21.6, heading); s.yaw.add(22.3, 178)
    s.hips(22.5, 1.3, ROOF_Y + 0.44, 22.02)
    s.bend.add(22.5, 46); s.look.add(22.4, 22); s.look.add(23.2, 24); s.turn.add(22.4, 0); s.turn.add(22.8, 20); s.turn.add(23.2, -14); s.turn.add(23.5, 0)
    s.feet["L"].move(22.25, 22.5, (1.43, ROOF_Y, 21.82), 0.05); s.feet["R"].move(22.05, 22.3, (1.17, ROOF_Y, 21.98), 0.05)
    # Jump.
    go, fall = 23.75, 0.88
    land = go + fall
    s.hips(23.45, 1.3, ROOF_Y + 0.40, 22.0)
    start, finish = Vector((1.32, ROOF_Y + 0.74, 21.76)), Vector((1.45, 0.80, 20.35))
    gravity = 9.81
    rise = (finish.y - start.y + 0.5 * gravity * fall * fall) / fall
    for i in range(15):
        u = i / 14
        s.hips(go + u * fall, start.x + (finish.x - start.x) * u, start.y + rise * u * fall - 0.5 * gravity * (u * fall) ** 2,
               start.z + (finish.z - start.z) * u)
    s.feet["R"].move(23.55, 23.72, (1.19, ROOF_Y, 21.74), 0.05)
    tuck = Track((go, -0.72), (go + 0.3, -0.50), (go + 0.6, -0.62), (land, -0.80))

    def in_air(side):
        sx, ahead = (0.13, -0.16) if side == "L" else (-0.13, -0.02)
        return lambda t: (s.hx(t) + sx, s.hy(t) + tuck(t), s.hz(t) + ahead)
    for side in "LR":
        s.feet[side].move(go, go + 0.22, in_air(side))
        sx, z = (0.13, 20.18) if side == "L" else (-0.13, 20.32)
        s.feet[side].move(land - 0.035, land, (1.45 + sx, 0, z))
        for dt, pitch in ((-0.05, 12), (0.1, 40), (0.6, 10), (fall, 0)): s.foot_pitch[side].add(go + dt, pitch)
    events.append((land, "land"))
    s.bend.add(go - 0.15, 52); s.bend.add(go + 0.10, 12); s.bend.add(go + 0.5, 22); s.bend.add(land, 26); s.bend.add(land + 0.2, 70)
    s.look.add(go, -6); s.look.add(go + 0.5, 20); s.look.add(land + 0.2, -30); s.look.add(25.4, -34)
    reach = {side: (lambda t, k=k: (s.hx(t) + 0.46 * k, s.hy(t) + 0.72, s.hz(t) - 0.12)) for side, k in (("L", 1), ("R", -1))}
    for side in "LR":
        s.hands[side].move(go - 0.4, go - 0.05, reach[side])
        s.hold[side].add(go - 0.12, 0); s.hold[side].add(go + 0.2, 0.85); s.hold[side].add(land - 0.1, 0.85)
    # Absorb the landing, one hand on the ground.
    s.hands["R"].move(land - 0.12, land + 0.16, (1.24, 0.17, 19.84)); s.hands["L"].move(land - 0.12, land + 0.16, (1.66, 0.46, 19.98))
    for side in "LR": s.hold[side].add(land + 0.16, 1); s.hold[side].add(land + 0.55, 1); s.hold[side].add(land + 0.95, 0)
    s.hips(land + 0.16, 1.45, 0.34, 20.28); s.hips(land + 0.5, 1.45, 0.34, 20.28)
    # Creep behind the flower pot.
    s.hips(25.6, 1.45, 0.56, 20.25); s.bend.add(25.6, 44)
    s.yaw.add(25.3, 178); s.yaw.add(25.7, 174)
    s.walk(25.6, 27.2, (1.45, 20.25), (SPAWN[0], SPAWN[2]), 4, 0.56, first="L", lift=0.10, bob=0.02, sound="grass", yaw=174, width=0.12)
    s.look.add(26.6, -30); s.look.add(27.6, -20); s.turn.add(27.3, 0); s.turn.add(27.7, -22); s.turn.add(28.3, 8)
    s.hips(27.8, SPAWN[0], 0.50, SPAWN[2]); s.bend.add(27.8, 38); s.yaw.add(27.9, 180)
    return s


# ------------------------------------------------------------------------------------------ baking

def rot_x(d): return Matrix.Rotation(radians(d), 3, "X")
def rot_y(d): return Matrix.Rotation(radians(d), 3, "Y")
def rot_z(d): return Matrix.Rotation(radians(d), 3, "Z")


def frame_from(direction, pole):
    direction = direction.normalized()
    side = direction.cross(pole)
    if side.length < 1e-5: side = direction.orthogonal()
    side.normalize()
    return Matrix((direction, side, side.cross(direction))).transposed()


def two_bone(root, target, upper, lower, pole):
    """Returns the middle joint and the clamped end."""
    reach = target - root
    distance = min(max(reach.length, abs(upper - lower) + 0.02), (upper + lower) * 0.998)
    direction = reach.normalized()
    bend = pole - direction * pole.dot(direction)
    bend.normalize()
    along = (upper * upper - lower * lower + distance * distance) / (2 * distance)
    away = sqrt(max(0.0, upper * upper - along * along))
    return root + direction * along + bend * away, root + direction * distance, reach.length - distance


class Baker:
    def __init__(self, rig):
        self.rig = rig
        self.rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
        self.parent = {b.name: b.parent.name if b.parent else None for b in rig.data.bones}
        self.order = [b.name for b in rig.data.bones]
        self.miss = {}

    def head(self, name): return self.rest[name].translation

    def pose(self, scene, t):
        rest = self.head
        spin = rot_z(-scene.yaw(t))
        hips_turn = spin @ rot_x(scene.hip_pitch(t)) @ rot_y(scene.hip_roll(t))
        hips = unity(scene.hx(t), scene.hy(t), scene.hz(t))
        bend, twist = scene.bend(t), scene.twist(t)
        swing = scene.swing_amount(t) * scene.swing(t)
        turns, heads = {"Root": Matrix.Identity(3)}, {"Root": Vector((0, 0, 0))}
        turns["Hips"], heads["Hips"] = hips_turn, hips

        def follow(child, parent, turn):
            heads[child] = heads[parent] + turns[parent] @ (rest(child) - rest(parent))
            turns[child] = turn
        follow("Spine", "Hips", hips_turn @ rot_x(bend * 0.45) @ rot_z(twist * 0.4))
        follow("Chest", "Spine", hips_turn @ rot_x(bend) @ rot_z(twist + swing * 7))
        follow("Neck", "Chest", hips_turn @ rot_x(bend * 0.55 + scene.look(t) * 0.4) @ rot_z((twist + scene.turn(t)) * 0.45))
        follow("Head", "Neck", hips_turn @ rot_x(bend * 0.12 + scene.look(t)) @ rot_z(twist * 0.4 + scene.turn(t)))

        for side, s in (("L", 1), ("R", -1)):
            # Legs: knees point where the foot and body face.
            upper, lower, foot = "UpperLeg_" + side, "LowerLeg_" + side, "Foot_" + side
            socket = heads["Hips"] + turns["Hips"] @ (rest(upper) - rest("Hips"))
            ground = scene.feet[side](t)
            pitch = scene.foot_pitch[side](t)
            foot_turn = rot_z(-scene.foot_yaw[side](t)) @ rot_x(pitch)
            # A pitched foot pivots on its toe (heel up) or heel (toe up) instead of sinking into the floor.
            lift = 0.17 * sin(radians(max(0.0, pitch))) + 0.06 * sin(radians(max(0.0, -pitch)))
            ankle = unity(*ground) + Vector((0, 0, ANKLE + lift))
            pole = rot_z(-(scene.yaw(t) + scene.foot_yaw[side](t)) / 2) @ Vector((s * 0.16, -1, 0.10))
            knee, ankle, miss = two_bone(socket, ankle, 0.43, 0.41, pole)
            if miss > 0.012: self.miss[side + "leg"] = max(self.miss.get(side + "leg", (0, 0)), (miss, t))
            neutral = frame_from(Vector((0, 0, -1)), Vector((0, -1, 0)))
            heads[upper], turns[upper] = socket, frame_from(knee - socket, pole) @ neutral.transposed()
            heads[lower], turns[lower] = knee, frame_from(ankle - knee, pole) @ neutral.transposed()
            heads[foot], turns[foot] = ankle, foot_turn

            # Arms: a relaxed swing, blended with whatever the hand is holding.
            upper, lower, hand = "UpperArm_" + side, "LowerArm_" + side, "Hand_" + side
            shoulder = heads["Chest"] + turns["Chest"] @ (rest(upper) - rest("Chest"))
            body = spin @ rot_x(bend * 0.35)
            crouch = min(1.0, max(0.0, bend - 10) / 30)
            forward = -s * swing * (0.20 - 0.06 * crouch)
            relaxed = shoulder + body @ Vector((s * (0.07 + 0.05 * crouch), -(0.07 + 0.16 * crouch) - forward, -(0.50 - 0.10 * crouch)))
            weight = scene.hold[side](t)
            wrist = relaxed
            if side in scene.hands and weight > 0.001:
                wrist = relaxed.lerp(unity(*scene.hands[side](t)) + spin @ Vector((0, PALM * 0.9, -0.03)), smooth(weight))
            # Holding something: elbows hang down and out. Otherwise they point back.
            loose, loaded = turns["Chest"] @ Vector((s * 0.55, 0.9, -0.5)), spin @ Vector((s * 0.55, 0.25, -0.9))
            pole = loose.lerp(loaded, smooth(weight) if side in scene.hands else 0.0)
            elbow, wrist, miss = two_bone(shoulder, wrist, 0.29, 0.27, pole)
            if weight > 0.95 and miss > 0.02: self.miss[side + "arm"] = max(self.miss.get(side + "arm", (0, 0)), (miss, t))
            neutral = frame_from(Vector((0, 0, -1)), Vector((0, 1, 0)))
            heads[upper], turns[upper] = shoulder, frame_from(elbow - shoulder, pole) @ neutral.transposed()
            heads[lower], turns[lower] = elbow, frame_from(wrist - elbow, pole) @ neutral.transposed()
            curl = turns[lower] @ rot_x(-18)
            grip = frame_from(spin @ Vector((0, -0.9, 0.35)), spin @ Vector((0, 0.35, 0.9))) @ neutral.transposed()
            blend = curl.to_quaternion().slerp(grip.to_quaternion(), smooth(weight) if scene.hold[side](t) > 0 and self.gripping(scene, t) else 0)
            heads[hand], turns[hand] = wrist, blend.to_matrix()
        return heads, turns

    @staticmethod
    def gripping(scene, t): return 9.0 <= t < 18.5 or 5.2 < t < 7.0

    def bake(self, scenes):
        bones = self.rig.pose.bones
        for bone in bones: bone.rotation_mode = "QUATERNION"
        last = {}
        for frame in range(int(round(LENGTH * FPS)) + 1):
            t = frame / FPS
            scene = next(s for s in reversed(scenes) if t >= s.t0 - 1e-6)
            heads, turns = self.pose(scene, t)
            world = {n: Matrix.Translation(heads[n]) @ turns[n].to_4x4() @ self.rest[n].to_3x3().to_4x4() for n in self.order}
            for name in self.order:
                parent = self.parent[name]
                if parent is None: basis = self.rest[name].inverted() @ world[name]
                else: basis = (self.rest[parent].inverted() @ self.rest[name]).inverted() @ (world[parent].inverted() @ world[name])
                quaternion = basis.to_quaternion()
                if name in last and last[name].dot(quaternion) < 0: quaternion.negate()
                last[name] = quaternion
                bones[name].rotation_quaternion = quaternion
                bones[name].keyframe_insert("rotation_quaternion", frame=frame, group=name)
                if name in ("Root", "Hips"):
                    bones[name].location = basis.translation
                    bones[name].keyframe_insert("location", frame=frame, group=name)


def main():
    rig = bpy.data.objects["Thief_Rig"]
    scene = bpy.context.scene
    scene.render.fps, scene.frame_start, scene.frame_end = FPS, 0, int(round(LENGTH * FPS))
    old = bpy.data.actions.get(ACTION)
    if old: bpy.data.actions.remove(old)
    action = bpy.data.actions.new(ACTION)
    action.use_fake_user = True
    rig.animation_data_create().action = action
    baker = Baker(rig)
    baker.bake([leave_the_van(), ladder_roof_and_jump()])
    print("MISSES", {k: (round(v[0], 3), round(v[1], 2)) for k, v in baker.miss.items()})
    with open(os.path.join(HERE, "intro_events.json"), "w", newline="\n") as file:
        json.dump(sorted((round(t, 3), kind) for t, kind in events), file)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points: key.interpolation = "LINEAR"
    bpy.ops.wm.save_mainfile()
    target = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Models", "Intro", "MuseumThief.fbx"))
    os.makedirs(os.path.dirname(target), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for name in ("Thief_Rig", "Thief_Mesh"): bpy.data.objects[name].select_set(True)
    bpy.ops.export_scene.fbx(filepath=target, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             axis_forward="-Z", axis_up="Y", add_leaf_bones=False, use_armature_deform_only=False,
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, mesh_smooth_type="FACE")
    print("EXPORTED", target)


main()
