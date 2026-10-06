"""A small Standard MIDI File writer for the game's music (format 1, one track per part).

Times are in beats. Parts can be swung (off-beat eighths pushed late) and humanised (tiny timing
and velocity drift, seeded so every render is identical).
"""
import random
import struct

PPQ = 960


def swing(t, ratio):
    """Moves off-beat eighths late: with ratio 0.62, an eighth at x.5 lands at x.62."""
    if ratio <= 0.5:
        return t
    beat = int(t // 1)
    frac = t - beat
    if frac <= 0.5:
        return beat + frac / 0.5 * ratio
    return beat + ratio + (frac - 0.5) / 0.5 * (1.0 - ratio)


class Part:
    def __init__(self, name, channel, program, bank=0, volume=100, pan=64, reverb=40, chorus=0,
                 swing=0.0, humanize=0.0, seed=1):
        self.name, self.channel, self.program, self.bank = name, channel, program, bank
        self.volume, self.pan, self.reverb, self.chorus = volume, pan, reverb, chorus
        self.swing, self.humanize = swing, humanize
        self.notes = []      # (start, length, pitch, velocity)
        self.controls = []   # (time, controller, value)
        self._rng = random.Random(seed)

    def note(self, start, length, pitch, velocity=80):
        self.notes.append((start, length, int(pitch), int(velocity)))

    def chord(self, start, length, pitches, velocity=70, roll=0.0):
        """Several notes at once; roll spreads them upward by that many beats each (a strum)."""
        for i, p in enumerate(sorted(pitches)):
            self.note(start + i * roll, length - i * roll, p, velocity)

    def cc(self, time, controller, value):
        self.controls.append((time, controller, int(max(0, min(127, value)))))

    def ramp(self, start, end, controller, v0, v1, steps=16):
        """A smooth controller move, e.g. an expression (11) swell."""
        for i in range(steps + 1):
            k = i / steps
            self.cc(start + (end - start) * k, controller, v0 + (v1 - v0) * k)

    def events(self, beats_to_seconds):
        """(tick, order, bytes): note-offs sort before note-ons at the same tick."""
        out = []
        ch = self.channel
        out.append((0, 0, bytes([0xB0 | ch, 0, self.bank])))
        out.append((0, 0, bytes([0xC0 | ch, self.program])))
        out.append((0, 0, bytes([0xB0 | ch, 7, self.volume])))
        out.append((0, 0, bytes([0xB0 | ch, 10, self.pan])))
        out.append((0, 0, bytes([0xB0 | ch, 91, self.reverb])))
        out.append((0, 0, bytes([0xB0 | ch, 93, self.chorus])))
        out.append((0, 0, bytes([0xB0 | ch, 11, 127])))
        for t, c, v in self.controls:
            out.append((ticks(t), 1, bytes([0xB0 | ch, c, v])))
        for start, length, pitch, vel in self.notes:
            s, e = swing(start, self.swing), swing(start + length, self.swing)
            if self.humanize:
                # Drift in seconds, turned back into beats at this point of the tempo map.
                spb = beats_to_seconds(s + 1) - beats_to_seconds(s)
                s += self._rng.gauss(0.0, self.humanize) / spb
                vel += self._rng.randint(-5, 5)
            s = max(0.0, s)
            e = max(s + 0.02, e - 0.01)
            vel = max(1, min(127, vel))
            out.append((ticks(s), 2, bytes([0x90 | ch, pitch, vel])))
            out.append((ticks(e), 0, bytes([0x80 | ch, pitch, 0])))
        return out


def ticks(beats):
    return int(round(beats * PPQ))


def varlen(n):
    data = [n & 0x7F]
    n >>= 7
    while n:
        data.insert(0, (n & 0x7F) | 0x80)
        n >>= 7
    return bytes(data)


class Song:
    def __init__(self, bpm):
        self.tempos = [(0.0, bpm)]   # (beat, bpm), changes take effect from that beat
        self.parts = []

    def tempo(self, beat, bpm):
        self.tempos.append((beat, bpm))
        self.tempos.sort()

    def part(self, *args, **kwargs):
        p = Part(*args, **kwargs)
        self.parts.append(p)
        return p

    def seconds(self, beat):
        """Where a beat falls in seconds, through the tempo changes."""
        t, prev_beat, prev_bpm = 0.0, 0.0, self.tempos[0][1]
        for b, bpm in self.tempos[1:]:
            if b >= beat:
                break
            t += (b - prev_beat) * 60.0 / prev_bpm
            prev_beat, prev_bpm = b, bpm
        return t + (beat - prev_beat) * 60.0 / prev_bpm

    def write(self, path):
        tracks = []
        tempo_track = []
        for b, bpm in self.tempos:
            mpq = int(round(60_000_000 / bpm))
            tempo_track.append((ticks(b), 0, bytes([0xFF, 0x51, 0x03]) + mpq.to_bytes(3, "big")))
        tracks.append(tempo_track)
        for p in self.parts:
            tracks.append(p.events(self.seconds))
        with open(path, "wb") as f:
            f.write(b"MThd" + struct.pack(">IHHH", 6, 1, len(tracks), PPQ))
            for events in tracks:
                events.sort(key=lambda e: (e[0], e[1]))
                body, now = bytearray(), 0
                for tick, _, data in events:
                    body += varlen(tick - now) + data
                    now = tick
                body += varlen(0) + bytes([0xFF, 0x2F, 0x00])
                f.write(b"MTrk" + struct.pack(">I", len(body)) + body)
