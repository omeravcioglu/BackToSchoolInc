"""The game's music, written out as notes: a cozy swing theme in F major for the campus, a gentle
version of the same tune for the title screen, and the trailer cut, which builds the theme up
to a key change and ends on the title.

Instruments are General MIDI presets from the GeneralUser GS soundfont (see render.py).
Melodies sit around C5 (262-784 Hz) with real bass under them: warm, never whistly.
"""
from midi import Song

# --- Pitches and chords -------------------------------------------------------------------------

NAMES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def n(name):
    """'Bb4' -> 70. Middle C is C4 (60)."""
    pc = NAMES[name[0]]
    rest = name[1:]
    while rest and rest[0] in "#b":
        pc += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 12 * (int(rest) + 1) + pc


QUALITIES = {
    "maj7": (0, 4, 7, 11), "maj9": (0, 4, 7, 11, 14), "6": (0, 4, 7, 9), "69": (0, 4, 7, 9, 14),
    "m7": (0, 3, 7, 10), "m9": (0, 3, 7, 10, 14), "m6": (0, 3, 7, 9), "7": (0, 4, 7, 10),
    "9": (0, 4, 7, 10, 14), "7sus4": (0, 5, 7, 10), "": (0, 4, 7), "m": (0, 3, 7),
}


def chord(symbol):
    """'Gm7' -> (root pitch class, intervals)."""
    root = NAMES[symbol[0]]
    q = symbol[1:]
    if q and q[0] in "#b":
        root += 1 if q[0] == "#" else -1
        q = q[1:]
    return root % 12, QUALITIES[q]


def bars_of(progression):
    """'Fmaj7 | Gm7 C7 | ...' -> per bar, a list of (symbol, start beat, beats)."""
    out = []
    for bar in progression.split("|"):
        names = bar.split()
        if not names:
            continue
        step = 4 / len(names)
        out.append([(s, i * step, step) for i, s in enumerate(names)])
    return out


def nearest(pc, around):
    """The pitch of pitch class pc closest to `around`."""
    base = around - ((around - pc) % 12)
    return base if around - base <= 6 else base + 12


# --- Accompaniment generators -------------------------------------------------------------------

# Every note of the tune (and its harmony line) as (start, end, pitch) in beats: the accompaniment
# reads it to stay out of the tune's way. Each song clears it before writing.
LEAD = []


def lead_at(start, end):
    return [p for s, e, p in LEAD if s < end - 0.05 and e > start + 0.05]


def rubs(q, tune):
    """A semitone (or minor ninth, or major seventh) against any tune note: sounds sour."""
    return any((q - p) % 12 in (1, 11) for p in tune)


def under_tune(notes, start, end, floor=48):
    """An accompaniment chord kept under the tune and out of its way: notes that rub against a
    tune note sounding then are dropped, notes at or above the tune's lowest note go down an
    octave (or are dropped if that would go below the floor)."""
    tune = lead_at(start, end)
    if not tune:
        return list(notes)
    lowest = min(tune)
    kept, moved = set(), []
    for q in notes:
        if rubs(q, tune):
            continue
        if q < lowest:
            kept.add(q)
            continue
        while q >= lowest and q - 12 >= floor:
            q -= 12
        if q < lowest:
            moved.append(q)
    for q in moved:   # a note moved down an octave must not land a semitone from another
        if all(abs(q - k) > 1 for k in kept):
            kept.add(q)
    return sorted(kept)


def walking_bass(part, bars, start_bar, velocity=88, two_feel=False, low=36, high=52):
    """An upright-bass line: chord roots on the downbeat, chord tones walking toward the next
    root, a chromatic approach note on the last beat. Two-feel plays roots and fifths in halves.
    A passing note that would rub against the tune is swapped for the nearest clean chord tone."""
    prev = n("F2")
    flat = [(sym, b, s, d) for b, bar in enumerate(bars) for sym, s, d in bar]

    def clean(p, t, length, root, ivs):
        tune = lead_at(t, t + length)
        if not rubs(p, tune):
            return p
        options = [root + iv + 12 * k for iv in ivs for k in (-2, -1, 0, 1, 2)]
        options = [q for q in options if low <= q <= high and not rubs(q, tune)]
        return min(options, key=lambda q: abs(q - p)) if options else p

    for i, (sym, b, s, d) in enumerate(flat):
        root, ivs = chord(sym)
        nxt_root = chord(flat[(i + 1) % len(flat)][0])[0]
        r = nearest(root, prev)
        while r < low:
            r += 12
        while r > high:
            r -= 12
        t = (start_bar + b) * 4 + s
        if two_feel:
            fifth = r + 7 if r + 7 <= high else r - 5
            part.note(t, min(d, 2) - 0.1, r, velocity)
            if d >= 4:
                part.note(t + 2, 1.9, clean(fifth, t + 2, 1.9, r, ivs), velocity - 6)
            prev = r
            continue
        target = nearest(nxt_root, r)
        while target < low:
            target += 12
        while target > high:
            target -= 12
        approach = target - 1 if abs(target - 1 - r) <= abs(target + 1 - r) else target + 1
        if target % 12 == r % 12:
            approach = r + 7 if r + 7 <= high else r - 5
        tones = [r + iv for iv in ivs[1:4]] + [r + iv - 12 for iv in ivs[1:4]]
        tones = [p for p in tones if low <= p <= high + 2]
        if d >= 4:
            # Root, then two chord tones heading toward the next root, then the approach.
            mid = sorted(tones, key=lambda p: abs(p - (r + target) / 2))
            a = mid[0] if mid else r + 7
            b2 = mid[1] if len(mid) > 1 else a
            if (target > r) != (b2 > a):
                a, b2 = b2, a
            line = [r, a, b2, approach]
        else:
            fifth = r + 7 if r + 7 <= high else r - 5
            line = [r, approach] if d == 2 else [r]
            if d == 2 and abs(approach - r) > 7:
                line = [r, fifth]
        for k, p in enumerate(line):
            if k > 0:
                p = clean(p, t + k, 0.9, r, ivs)
            accent = 6 if k == 0 else 0
            part.note(t + k, 0.9, p, velocity + accent - 4)
        prev = line[-1]


def voicing(sym, prev, low=53, high=72):
    """A four-note voicing (3rd, 5th or 13th, 7th, 9th: no root, the bass has it), placed as
    close as possible to the previous one so the chords glide, and never with two notes a
    semitone apart (that cluster sounds sour in soft, sustained instruments)."""
    root, ivs = chord(sym)
    third = ivs[1]
    seventh = ivs[3] if len(ivs) > 3 else (ivs[2] + 4)
    dominant = sym.endswith("7") and "maj" not in sym and "m" not in sym[1:3]
    color = 9 if dominant else ivs[2]

    def candidates(pcs):
        for base in range(low, high - 4):
            notes = sorted(nearest(pc, base + 3) for pc in pcs)
            if notes[0] < low or notes[-1] > high:
                continue
            if any(b - a == 1 for a, b in zip(notes, notes[1:])):
                continue
            yield notes

    pcs = sorted({(root + third) % 12, (root + color) % 12, (root + seventh) % 12, (root + 14) % 12})
    options = list(candidates(pcs))
    if not options:
        # The ninth can't avoid a semitone neighbour: use the root instead.
        pcs = sorted({(root + third) % 12, (root + color) % 12, (root + seventh) % 12, root % 12})
        options = list(candidates(pcs)) or [sorted(nearest(pc, 60) for pc in pcs)]
    if prev:
        return min(options, key=lambda v: sum(abs(a - b) for a, b in zip(v, prev)))
    return min(options, key=lambda v: abs(v[0] - 57))


# Comping rhythms for one bar (start, length): swung, sparse, leaving room for the tune.
COMPS = [
    [(0.0, 1.4), (1.5, 0.4)],               # the Charleston
    [(0.5, 0.4), (2.0, 1.4)],
    [(1.5, 0.4), (3.0, 0.9)],
    [(0.0, 0.9), (2.5, 0.4), (3.5, 0.4)],
]


def piano_comp(part, bars, start_bar, velocity=58, pattern_offset=0):
    prev = None
    for b, bar in enumerate(bars):
        hits = COMPS[(b + pattern_offset) % len(COMPS)]
        for sym, s, d in bar:
            v = voicing(sym, prev)
            prev = v
            for hs, hl in hits:
                if s <= hs < s + d:
                    at = (start_bar + b) * 4 + hs
                    part.chord(at, hl, under_tune(v, at, at + hl), velocity + (4 if hs == 0 else 0), roll=0.012)


def held(part, start, length, notes, velocity, floor):
    """A sustained chord that stays clean under the tune: held whole if it can keep at least two
    notes for its full length, otherwise split in halves (down to single beats) where needed."""
    clean = under_tune(notes, start, start + length, floor)
    if len(clean) >= 2 or length <= 1.01:
        part.chord(start, length - 0.03, clean, velocity)
        return
    half = length / 2
    held(part, start, half, notes, velocity, floor)
    held(part, start + half, half, notes, velocity, floor)


def pad(part, bars, start_bar, velocity=48, low=53, high=69):
    """Sustained string chords, one per chord, voiced to glide and kept clean under the tune."""
    prev = None
    for b, bar in enumerate(bars):
        for sym, s, d in bar:
            v = voicing(sym, prev, low, high)
            prev = v
            held(part, (start_bar + b) * 4 + s, d, v, velocity, low - 5)


def pizz(part, bars, start_bar, velocity=62):
    """Pizzicato strings on the off-beats: light, bouncy chords under the tune."""
    prev = None
    for b, bar in enumerate(bars):
        for sym, s, d in bar:
            v = voicing(sym, prev, 55, 74)
            prev = v
            for off in (0.5, 1.5, 2.5, 3.5):
                if s <= off < s + d:
                    at = (start_bar + b) * 4 + off
                    part.chord(at, 0.4, under_tune(v[1:], at, at + 0.4, floor=50), velocity - (6 if off in (0.5, 2.5) else 0))


def sparkle(part, sym, at, velocity=40, up=True):
    """A quick celesta arpeggio through the chord between C5 and C6: root, 3rd, 5th and 9th (a
    major seventh ringing next to the root would rub)."""
    root, ivs = chord(sym)
    tones = [iv for iv in ivs[:4] if iv != 11] + ([14] if 11 in ivs else [])
    notes = sorted({nearest((root + iv) % 12, 76) for iv in tones})
    notes = [p for p in notes if 72 <= p <= 84] or [72]
    notes = [q for i, q in enumerate(notes) if i == 0 or q - notes[i - 1] > 1]
    if not up:
        notes = notes[::-1]
    for i, p in enumerate(notes + [notes[0] + 12 if up and notes[0] + 12 <= 84 else notes[-1]]):
        part.note(at + i * 0.25, 1.0, p, velocity - i * 2)


KEEP = {"Upright bass", "Bass", "Brushes", "end"}
# When two accompaniment notes clash, the first of these to appear loses its note.
EXPENDABLE = ["Celesta", "Pizzicato", "Strings", "Piano LH", "Piano", "Clarinet", "Flute", "Marimba", "Vibraphone"]


def tidy(song):
    """Removes accompaniment notes that sound a minor second or ninth against another note for
    a quarter beat or more. Tune notes (LEAD) and the bass always stay."""
    from midi import swing as swung
    lead = {(round(a, 4), round(b, 4), q) for a, b, q in LEAD}
    entries = []
    for part in song.parts:
        if part.name in ("Brushes", "end"):
            continue
        for i, (st, ln, pitch, vel) in enumerate(part.notes):
            protected = part.name in KEEP or (round(st, 4), round(st + ln, 4), pitch) in lead
            entries.append([swung(st, part.swing), swung(st + ln, part.swing), pitch, part, i, protected])
    entries.sort(key=lambda e: e[0])
    gone = set()
    for k, a in enumerate(entries):
        for b in entries[k + 1:]:
            if b[0] >= a[1]:
                break
            if (id(a[3]), a[4]) in gone or (id(b[3]), b[4]) in gone:
                continue
            if min(a[1], b[1]) - max(a[0], b[0]) < 0.25 or (max(a[2], b[2]) - min(a[2], b[2])) % 12 != 1:
                continue
            if a[5] and b[5]:
                continue
            if a[5] or b[5]:
                victim = b if a[5] else a
            else:
                rank = lambda e: EXPENDABLE.index(e[3].name) if e[3].name in EXPENDABLE else 99
                victim = a if rank(a) <= rank(b) else b
            gone.add((id(victim[3]), victim[4]))
    for part in song.parts:
        part.notes = [nt for i, nt in enumerate(part.notes) if (id(part), i) not in gone]
    return len(gone)


# Brush kit (GS/GM drum notes).
KICK, BRUSH_TAP, BRUSH_SLAP, SWIRL, PEDAL_HH, CRASH, RIDE, RIDE_BELL = 36, 38, 39, 40, 44, 49, 51, 53


def brushes(part, bar, level, fill=False, crash=False):
    """One bar of brushes. Level 0: a soft swirl; 1: swirl, feathered kick and hi-hat on 2 and 4;
    2: the swing ride pattern with brush slaps on 2 and 4."""
    t = bar * 4
    if crash:
        part.note(t, 3, CRASH, 70)
    if level == 0:
        part.note(t, 3.8, SWIRL, 56)
        return
    part.note(t, 0.5, KICK, 52)
    part.note(t + 2, 0.5, KICK, 46)
    part.note(t + 1, 0.5, PEDAL_HH, 72)
    part.note(t + 3, 0.5, PEDAL_HH, 76)
    if level == 1:
        part.note(t, 1.9, SWIRL, 64)
        part.note(t + 2, 1.9, SWIRL, 60)
        part.note(t + 1, 0.5, BRUSH_TAP, 54)
        part.note(t + 3, 0.5, BRUSH_TAP, 58)
    else:
        for s, v in ((0, 82), (1, 70), (1.5, 56), (2, 80), (3, 70), (3.5, 56)):
            part.note(t + s, 0.5, RIDE, v)
        part.note(t + 1, 0.5, BRUSH_SLAP, 70)
        part.note(t + 3, 0.5, BRUSH_SLAP, 74 if not fill else 60)
    if fill:
        # Brush taps rolling into the next phrase (triplets on beat 4).
        for i in range(3):
            part.note(t + 3 + i / 3, 0.3, BRUSH_TAP, 60 + 10 * i)


# --- The tune -----------------------------------------------------------------------------------

# Bar by bar: (start in the bar, length, pitch). Swung eighths, all inside E4..G5.
A_TUNE = [
    [(0, .5, "A4"), (.5, .5, "C5"), (1, 1, "F5"), (2, .5, "E5"), (2.5, 1.5, "C5")],
    [(0, .5, "D5"), (.5, .5, "C5"), (1, 1, "A4"), (2, 1, "F4"), (3, .5, "G4"), (3.5, .5, "A4")],
    [(0, .5, "Bb4"), (.5, .5, "D5"), (1, 1, "G5"), (2, .5, "F5"), (2.5, 1.5, "D5")],
    [(0, .5, "E5"), (.5, .5, "D5"), (1, 1, "C5"), (2, 1, "Bb4"), (3, 1, "G4")],
    [(0, .5, "A4"), (.5, .5, "C5"), (1, 1, "E5"), (2, .5, "G5"), (2.5, 1.5, "E5")],
    [(0, 1, "F#5"), (1, .5, "E5"), (1.5, .5, "D5"), (2, 1, "C5"), (3, 1, "A4")],
    [(0, .5, "D5"), (.5, .5, "Bb4"), (1, 1, "G4"), (2, .5, "C5"), (2.5, .5, "Bb4"), (3, .5, "G4"), (3.5, .5, "E4")],
    [(0, 2, "F4")],
]
A_CHORDS = "Fmaj7 | Dm7 | Gm7 | C7 | Am7 | D7 | Gm7 C7 | F6"
# The second A turns toward the bridge: F7 leads into B-flat.
A2_LAST = [(0, 1, "F4"), (1, .5, "A4"), (1.5, .5, "C5"), (2, 1, "Eb5"), (3, 1, "D5")]
A2_CHORDS = "Fmaj7 | Dm7 | Gm7 | C7 | Am7 | D7 | Gm7 C7 | F6 F7"

B_TUNE = [
    [(0, 1.5, "D5"), (1.5, .5, "C5"), (2, .5, "Bb4"), (2.5, .5, "A4"), (3, 1, "F4")],
    [(0, 1.5, "Db5"), (1.5, .5, "C5"), (2, .5, "Bb4"), (2.5, .5, "G4"), (3, 1, "F4")],
    [(0, 1, "C5"), (1, 1, "E5"), (2, 1.5, "E5"), (3.5, .5, "D5")],
    [(0, 1, "C5"), (1, 1, "A4"), (2, 1, "F#4"), (3, 1, "A4")],
    [(0, .5, "Bb4"), (.5, .5, "A4"), (1, .5, "G4"), (1.5, .5, "Bb4"), (2, 1, "D5"), (3, 1, "F5")],
    [(0, 1, "E5"), (1, .5, "D5"), (1.5, .5, "C5"), (2, 1, "Bb4"), (3, 1, "G4")],
    [(0, .5, "A4"), (.5, .5, "C5"), (1, 1, "E5"), (2, 1, "F5"), (3, 1, "D5")],
    [(0, 1, "Bb4"), (1, 1, "G4"), (2, 1, "E4"), (3, 1, "G4")],
]
B_CHORDS = "Bbmaj7 | Bbm6 | Am7 | D7 | Gm7 | C7 | Fmaj7 Dm7 | Gm7 C7"

# A lyrical, slower line for the breakdown (piano), over a turn toward D minor.
C_TUNE = [
    [(0, 1.5, "F5"), (1.5, .5, "E5"), (2, 2, "D5")],
    [(0, 1, "B4"), (1, 1, "D5"), (2, 1.5, "F5"), (3.5, .5, "E5")],
    [(0, 3, "E5"), (3, .5, "D5"), (3.5, .5, "C5")],
    [(0, 1, "C#5"), (1, 1, "E5"), (2, 1, "G5"), (3, 1, "E5")],
    [(0, 1.5, "F5"), (1.5, .5, "E5"), (2, 1, "D5"), (3, 1, "A4")],
    [(0, 1, "B4"), (1, .5, "C5"), (1.5, .5, "D5"), (2, 2, "F5")],
    [(0, 1.5, "D5"), (1.5, .5, "C5"), (2, 1, "Bb4"), (3, 1, "A4")],
    [(0, 1, "G4"), (1, 1, "Bb4"), (2, 1, "C5"), (3, 1, "G4")],
]
C_CHORDS = "Dm7 | G7 | Cmaj7 | A7 | Dm7 | G7 | Gm7 | C7"


def play(part, tune, start_bar, velocity=84, transpose=0, octave=0):
    """Writes a tune with a little phrasing: downbeats leaned on, off-beats lighter."""
    for b, bar in enumerate(tune):
        for s, length, name in bar:
            accent = 6 if s == 0 else 3 if s == 2 else -5 if s % 1 else 0
            at, pitch = (start_bar + b) * 4 + s, n(name) + transpose + 12 * octave
            part.note(at, length, pitch, velocity + accent)
            LEAD.append((at, at + length, pitch))


def harmony_below(tune, chords, min_gap=3, max_gap=9):
    """A second voice under the tune: the nearest chord tone 3 to 9 semitones below each note."""
    bars = bars_of(chords)
    out = []
    for b, bar in enumerate(tune):
        line = []
        for s, length, name in bar:
            p = n(name)
            sym = next(sym for sym, cs, cd in bars[b] if cs <= s < cs + cd)
            root, ivs = chord(sym)
            cands = [q for q in range(p - max_gap, p - min_gap + 1) if (q - root) % 12 in ivs]
            q = max(cands) if cands else p - 5
            line.append((s, length, q))
        out.append(line)
    return out


def play_pitches(part, tune, start_bar, velocity):
    for b, bar in enumerate(tune):
        for s, length, p in bar:
            at = (start_bar + b) * 4 + s
            part.note(at, length, p, velocity + (4 if s == 0 else 0))
            LEAD.append((at, at + length, p))


def transpose_chords(progression, semitones):
    names = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"]
    out = []
    for bar in progression.split("|"):
        syms = []
        for sym in bar.split():
            root, _ = chord(sym)
            q = sym[1:]
            if q and q[0] in "#b":
                q = q[1:]
            syms.append(names[(root + semitones) % 12] + q)
        out.append(" ".join(syms))
    return " | ".join(out)


def transpose_tune(tune, semitones):
    names = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"]
    out = []
    for bar in tune:
        line = []
        for s, length, name in bar:
            p = n(name) + semitones
            line.append((s, length, f"{names[p % 12]}{p // 12 - 1}"))
        out.append(line)
    return out


# --- Songs --------------------------------------------------------------------------------------

def band(song, swing=0.62):
    """The campus band: melody instruments, piano, bass, brushes and colour."""
    return dict(
        vibes=song.part("Vibraphone", 0, 11, volume=104, pan=78, reverb=58, swing=swing, humanize=0.006, seed=2),
        piano=song.part("Piano", 1, 0, volume=100, pan=52, reverb=46, swing=swing, humanize=0.007, seed=3),
        bass=song.part("Upright bass", 2, 32, volume=92, pan=64, reverb=18, swing=swing, humanize=0.005, seed=4),
        clarinet=song.part("Clarinet", 3, 71, volume=88, pan=40, reverb=50, swing=swing, humanize=0.008, seed=5),
        pizz=song.part("Pizzicato", 4, 45, volume=96, pan=92, reverb=44, swing=swing, humanize=0.006, seed=6),
        strings=song.part("Strings", 5, 49, volume=72, pan=64, reverb=72, seed=7),
        celesta=song.part("Celesta", 6, 8, volume=96, pan=96, reverb=70, swing=swing, humanize=0.004, seed=8),
        marimba=song.part("Marimba", 7, 12, volume=96, pan=70, reverb=50, swing=swing, humanize=0.006, seed=9),
        flute=song.part("Flute", 8, 73, volume=70, pan=58, reverb=56, swing=swing, humanize=0.008, seed=10),
        drums=song.part("Brushes", 9, 40, bank=128, volume=124, pan=64, reverb=36, swing=swing, humanize=0.004, seed=11),
    )


def campus():
    """The campus loop: A, A, B, A, C, A at 104 bpm, swung. 48 bars, about 111 seconds; the last
    bar's turnaround leads straight back into the first."""
    LEAD.clear()
    song = Song(104)
    p = band(song)
    sections = [  # (first bar, tune, chords, melody instrument, groove level)
        (0, A_TUNE, A_CHORDS, "vibes", 1),
        (8, A_TUNE[:7] + [A2_LAST], A2_CHORDS, "vibes", 2),
        (16, B_TUNE, B_CHORDS, "clarinet", 2),
        (24, A_TUNE, A_CHORDS, "marimba", 2),
        (32, C_TUNE, C_CHORDS, "piano", 1),
        (40, A_TUNE[:7] + [[(0, 1, "F4"), (1, .5, "A4"), (1.5, .5, "C5"), (2, 1, "D5"), (3, 1, "C5")]],
         "Fmaj7 | Dm7 | Gm7 | C7 | Am7 | D7 | Gm7 C7 | F6 C7", "vibes", 2),
    ]
    for first, tune, chords, lead, level in sections:
        bars = bars_of(chords)
        play(p[lead], tune, first, velocity=88 if lead != "piano" else 80)
        if lead == "marimba":
            play(p["flute"], tune, first, velocity=70)
        if first == 40:
            play_pitches(p["clarinet"], harmony_below(tune, chords), first, 64)
            pad(p["strings"], bars, first, velocity=44)
        if lead != "piano":
            piano_comp(p["piano"], bars, first, velocity=56 if level == 2 else 50, pattern_offset=first)
        else:
            pad(p["strings"], bars, first, velocity=46)
        walking_bass(p["bass"], bars, first, two_feel=(level == 1))
        if first in (8, 16, 40):
            pizz(p["pizz"], bars, first, velocity=58)
        for b in range(8):
            brushes(p["drums"], first + b, level, fill=(b == 7), crash=(b == 0 and first in (16, 40)))
        # Answers in the gaps: the clarinet in the second A, celesta at the phrase ends.
        if first == 0:
            for s, l, name in [(2, .5, "A4"), (2.5, .5, "C5"), (3, .5, "D5"), (3.5, .5, "C5")]:
                p["clarinet"].note((first + 7) * 4 + s, l, n(name), 62)
        sparkle(p["celesta"], bars[3][0][0], (first + 3) * 4 + 2.5, velocity=48)
        sparkle(p["celesta"], bars[7][0][0], (first + 7) * 4 + 2, velocity=52)
    tidy(song)
    return song, 48 * 4


def title():
    """The title screen: the same tune, slow and straight, on piano with strings. 16 bars at
    80 bpm, about 48 seconds."""
    LEAD.clear()
    song = Song(80)
    piano = song.part("Piano", 0, 0, volume=96, pan=60, reverb=64, humanize=0.008, seed=21)
    left = song.part("Piano LH", 1, 0, volume=80, pan=56, reverb=64, humanize=0.008, seed=22)
    strings = song.part("Strings", 2, 49, volume=70, pan=64, reverb=80, seed=23)
    bass = song.part("Bass", 3, 32, volume=96, pan=64, reverb=30, seed=24)
    celesta = song.part("Celesta", 4, 8, volume=62, pan=90, reverb=80, humanize=0.004, seed=25)
    chords = A_CHORDS.replace("F6", "F6 C7sus4") + " | Fmaj7 | Am7 | Bbmaj7 | Bbm6 | Am7 | D7 | Gm7 C7 | F69"
    bars = bars_of(chords)
    tune = A_TUNE + [
        [(0, 1, "C5"), (1, 1, "A4"), (2, 2, "E5")],
        [(0, 1, "E5"), (1, 1, "C5"), (2, 2, "G4")],
        [(0, 1, "D5"), (1, 1, "F5"), (2, 1.5, "A5"), (3.5, .5, "F5")],
        [(0, 2, "Db5"), (2, 2, "Bb4")],
        [(0, 1, "C5"), (1, 1, "E5"), (2, 2, "G5")],
        [(0, 1.5, "F#5"), (1.5, .5, "E5"), (2, 2, "D5")],
        [(0, 1, "Bb4"), (1, 1, "D5"), (2, 1, "C5"), (3, 1, "E4")],
        [(0, 4, "F4")],
    ]
    play(piano, tune, 0, velocity=82)
    prev = None
    for b, bar in enumerate(bars):
        for sym, s, d in bar:
            root, ivs = chord(sym)
            # Left hand: a rolling pattern, root - fifth - tenth - fifth, under the pedal.
            r = nearest(root, n("F3") if root >= 5 else n("C3"))
            pattern = [r, r + 7, r + 12 + ivs[1], r + 7]
            steps = int(d * 2)
            for k in range(steps):
                at = b * 4 + s + k * 0.5
                q = pattern[k % 4]
                if rubs(q, lead_at(at, b * 4 + s + d)):
                    q = r + 7 if not rubs(r + 7, lead_at(at, b * 4 + s + d)) else r
                left.note(at, 0.5 * (steps - k), q, 58 - (k % 2) * 6)
            left.cc(b * 4 + s, 64, 127)
            left.cc(b * 4 + s + d - 0.05, 64, 0)
            bass.note(b * 4 + s, d - 0.1, nearest(root, n("F2")) - (12 if nearest(root, n("F2")) > n("C3") else 0), 64)
    pad(strings, bars, 0, velocity=40)
    strings.ramp(0, 8, 11, 70, 110)
    strings.ramp(56, 64, 11, 110, 80)
    sparkle(celesta, "Fmaj7", 7 * 4 + 2, velocity=44)
    sparkle(celesta, "F69", 15 * 4 + 1, velocity=44)
    tidy(song)
    return song, 16 * 4


# The trailer: (shot, bars). The cuts land on bar lines; render.py and the recorder share this.
TRAILER_BPM = 112
TRAILER_SHOTS = [
    ("title", 2),
    ("young_school", 2), ("follow", 2), ("person_card", 2), ("inbox", 2),
    ("build", 2), ("life_Playground", 1.5), ("life_Cafeteria", 1.5), ("life_Classroom", 1.5), ("life_PracticeField", 1.5),
    ("page_Students", 1), ("page_Staff", 1), ("page_Money", 1), ("page_Ranks", 1),
    ("level_up", 2),
    ("pull_back", 4),
    ("end_card", 3),
]


def trailer():
    """The trailer cut, 31 bars at 112 bpm: a piano intro on the title, the tune twice as the
    school comes to life, quick bars for the pages, a lift on the level-up card, the tune a whole
    step higher over the whole campus, and the title chord ringing out on the end card."""
    LEAD.clear()
    song = Song(TRAILER_BPM)
    p = band(song, swing=0.6)
    # 1. Intro (bars 0-1): piano alone plays the opening of the tune, celesta answers.
    intro = [A_TUNE[0], [(0, .5, "D5"), (.5, .5, "C5"), (1, 1, "A4"), (2, 2, "G4")]]
    play(p["piano"], intro, 0, velocity=74)
    for b, sym in enumerate(["Fmaj7", "C7sus4"]):
        held(p["piano"], b * 4, 4, voicing(sym, None, 50, 66), 46, 45)
        p["bass"].note(b * 4 + 2, 1.8, n("F2") if b == 0 else n("C2"), 60)
    p["drums"].note(4, 3.8, SWIRL, 30)
    sparkle(p["celesta"], "Fmaj7", 1 * 4 + 2, velocity=48)
    # 2. The tune (bars 2-9): vibes with bass in two and soft brushes.
    bars = bars_of(A_CHORDS)
    play(p["vibes"], A_TUNE, 2, velocity=86)
    piano_comp(p["piano"], bars, 2, velocity=50)
    walking_bass(p["bass"], bars, 2, two_feel=True)
    for b in range(8):
        brushes(p["drums"], 2 + b, 1, fill=(b == 7))
    sparkle(p["celesta"], "C7", 5 * 4 + 2.5, velocity=46)
    # 3. Again, fuller (bars 10-17): walking bass, ride, pizzicato, clarinet answers.
    bars = bars_of(A2_CHORDS)
    play(p["vibes"], A_TUNE[:7] + [A2_LAST], 10, velocity=90)
    piano_comp(p["piano"], bars, 10, velocity=56, pattern_offset=1)
    walking_bass(p["bass"], bars, 10)
    pizz(p["pizz"], bars, 10, velocity=58)
    for b in range(8):
        brushes(p["drums"], 10 + b, 2, fill=(b == 7), crash=(b == 0))
    for s, l, name in [(2, .5, "A4"), (2.5, .5, "C5"), (3, .5, "D5"), (3.5, .5, "C5")]:
        p["clarinet"].note(9 * 4 + s, l, n(name), 62)
    # 4. The pages (bars 18-21): the bridge's first half, clarinet lead, pizzicato bounce.
    bars = bars_of("Bbmaj7 | Bbm6 | Am7 | D7")
    play(p["clarinet"], B_TUNE[:4], 18, velocity=86)
    piano_comp(p["piano"], bars, 18, velocity=54)
    walking_bass(p["bass"], bars, 18)
    pizz(p["pizz"], bars, 18, velocity=62)
    for b in range(4):
        brushes(p["drums"], 18 + b, 2, crash=(b == 0))
    # 5. The lift (bars 22-23): the bridge's D7 slips up to E-flat, then D7 again, now the
    # dominant of G: a rising line and a drum roll into the new key.
    bars = bars_of("Ebmaj7 | D7sus4 D7")
    for i, name in enumerate(["G4", "A4", "Bb4", "C5", "D5", "Eb5", "F5", "G5"]):
        p["vibes"].note(22 * 4 + i * 0.5, 0.5, n(name), 72 + i * 3)
    for i, name in enumerate(["A4", "C5", "D5", "F#5"]):
        p["clarinet"].note(23 * 4 + i, 1, n(name) - 12, 68 + i * 3)
    piano_comp(p["piano"], bars, 22, velocity=58)
    walking_bass(p["bass"], bars, 22)
    p["strings"].chord(22 * 4, 3.95, [n("Eb3"), n("Bb3"), n("G4")], 50)
    p["strings"].chord(23 * 4, 3.95, [n("D3"), n("A3"), n("C4"), n("F#4")], 54)
    p["strings"].ramp(22 * 4, 24 * 4, 11, 60, 125)
    brushes(p["drums"], 22, 2)
    for i in range(12):  # brush taps building through the last bar
        p["drums"].note(23 * 4 + i / 3, 0.3, BRUSH_TAP, 34 + i * 3)
    # 6. The climax (bars 24-27): the tune a whole step up, in G, everyone in.
    up = 2
    chords = transpose_chords("Fmaj7 | Dm7 | Gm7 | C7", up)
    bars = bars_of(chords)
    tune = transpose_tune(A_TUNE[:4], up)
    play(p["vibes"], tune, 24, velocity=94)
    play_pitches(p["clarinet"], harmony_below(tune, chords), 24, 70)
    pad(p["strings"], bars, 24, velocity=50)
    piano_comp(p["piano"], bars, 24, velocity=60, pattern_offset=2)
    walking_bass(p["bass"], bars, 24, velocity=94)
    pizz(p["pizz"], bars, 24, velocity=60)
    for b in range(4):
        brushes(p["drums"], 24 + b, 2, crash=(b == 0), fill=(b == 3))
    sparkle(p["celesta"], "Gmaj7", 25 * 4 + 2.5, velocity=52)
    # 7. The end card (bars 28-30): cadence home in G, slowing, the last chord left to ring.
    bars = bars_of("Am7 D7 | G69 | G69")
    end_tune = [[(0, 1, "C5"), (1, 1, "A4"), (2, 1, "F#4"), (3, 1, "A4")], [(0, 4, "G4")], []]
    play(p["vibes"], end_tune, 28, velocity=84)
    v = voicing("Am7", None, 52, 70)
    p["piano"].chord(28 * 4, 1.9, under_tune(v, 28 * 4, 28 * 4 + 2), 56, roll=0.02)
    v2 = voicing("D7", v, 52, 70)
    p["piano"].chord(28 * 4 + 2, 1.9, under_tune(v2, 28 * 4 + 2, 29 * 4), 56, roll=0.02)
    # G6/9 to finish: warm, and nothing a semitone from the tune's last G.
    final = [n("G2"), n("D3"), n("B3"), n("E4"), n("A4"), n("D5")]
    p["piano"].chord(29 * 4, 7.5, final, 60, roll=0.04)
    p["piano"].cc(29 * 4, 64, 127)
    held(p["strings"], 28 * 4, 4, voicing("Am7", None, 52, 70), 48, 47)
    p["strings"].chord(29 * 4, 7.8, [n("G3"), n("B3"), n("D4"), n("E4")], 50)
    p["strings"].ramp(29 * 4 + 2, 31 * 4, 11, 120, 40)
    p["bass"].note(28 * 4, 1.9, n("A2"), 84)
    p["bass"].note(28 * 4 + 2, 1.9, n("D2"), 84)
    p["bass"].note(29 * 4, 7.5, n("G1"), 84)
    p["drums"].note(29 * 4, 6, CRASH, 46)
    p["drums"].note(29 * 4, 7, SWIRL, 30)
    sparkle(p["celesta"], "G69", 29 * 4 + 0.5, velocity=54)
    sparkle(p["celesta"], "G69", 30 * 4 + 0.5, velocity=44, up=False)
    song.tempo(28 * 4, 108)
    song.tempo(28 * 4 + 2, 100)
    song.tempo(29 * 4, 92)
    tidy(song)
    return song, 31 * 4


def trailer_cuts():
    """Each shot's start and length in seconds, from the tempo map."""
    song, _ = trailer()
    out, beat = [], 0.0
    for name, bars in TRAILER_SHOTS:
        start = song.seconds(beat)
        beat += bars * 4
        out.append((name, start, song.seconds(beat) - start))
    return out
