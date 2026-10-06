"""The game's sound effects, played on the same instruments as the music so they belong together:
marimba, vibraphone, celesta, harp, bassoon, piano and brushes, all between C3 and G5 so nothing
is shrill. Each is a tiny MIDI score; render.py plays it through FluidSynth, trims it and sets its
peak level (quiet for the clicks you make all the time, louder for news and celebrations).
"""
from midi import Song
from songs import n

# Brush kit and standard kit drum notes.
LOW_TOM, SLAP, CRASH, LOW_WOOD = 41, 39, 49, 77


def _song(bpm=120):
    return Song(bpm)


def tap():
    """A button: a soft marimba tock."""
    s = _song()
    m = s.part("Marimba", 0, 12, volume=110, reverb=12)
    m.note(0, 0.25, n("E5"), 72)
    return s


def select():
    """Picking a room or a person: one round vibraphone note."""
    s = _song()
    v = s.part("Vibraphone", 0, 11, volume=110, reverb=20)
    v.note(0, 0.5, n("C5"), 64)
    return s


def page():
    """Turning to a page: a quick harp flourish up a pentatonic scale."""
    s = _song()
    h = s.part("Harp", 0, 46, volume=110, reverb=30)
    for i, name in enumerate(["C4", "D4", "E4", "G4", "A4"]):
        h.note(i * 0.07, 1.0, n(name), 58 + i * 3)
    return s


def pop():
    """A note at the bottom of the screen: vibraphone, a third up."""
    s = _song()
    v = s.part("Vibraphone", 0, 11, volume=110, reverb=24)
    v.note(0, 0.3, n("C5"), 66)
    v.note(0.18, 0.6, n("E5"), 70)
    return s


def bell():
    """A letter arrives: a celesta ding-dong."""
    s = _song()
    c = s.part("Celesta", 0, 8, volume=120, reverb=36)
    c.note(0, 1.0, n("E5"), 84)
    c.note(0.45, 1.6, n("C5"), 80)
    return s


def coin():
    """Money spent on land: a quick marimba arpeggio over a pizzicato pluck."""
    s = _song()
    m = s.part("Marimba", 0, 12, volume=115, reverb=20)
    p = s.part("Pizzicato", 1, 45, volume=100, reverb=20)
    for i, name in enumerate(["C5", "E5", "G5"]):
        m.note(i * 0.12, 0.6, n(name), 74 + i * 4)
    p.note(0, 0.5, n("C4"), 70)
    return s


def build():
    """A room placed: a low marimba and tom thunk, a brush slap on top."""
    s = _song()
    m = s.part("Marimba", 0, 12, volume=120, reverb=18)
    d = s.part("Drums", 9, 40, bank=128, volume=110, reverb=18)
    m.chord(0, 0.8, [n("C3"), n("G3")], 92)
    d.note(0, 0.5, LOW_TOM, 80)
    d.note(0.02, 0.4, SLAP, 60)
    m.note(0.3, 0.6, n("C4"), 70)
    return s


def error():
    """Something can't be done: a gentle bassoon uh-oh, two notes down."""
    s = _song()
    b = s.part("Bassoon", 0, 70, volume=110, reverb=16)
    b.note(0, 0.28, n("A2"), 78)
    b.note(0.32, 0.42, n("F2"), 74)
    return s


def chime():
    """The assistant speaks: two soft vibraphone notes."""
    s = _song()
    v = s.part("Vibraphone", 0, 11, volume=110, reverb=30)
    v.note(0, 0.5, n("G4"), 58)
    v.note(0.22, 1.0, n("D5"), 62)
    return s


def demolish():
    """A room taken down: a low tom and slap, pizzicato tumbling down."""
    s = _song()
    d = s.part("Drums", 9, 40, bank=128, volume=110, reverb=20)
    p = s.part("Pizzicato", 1, 45, volume=110, reverb=20)
    d.note(0, 0.5, LOW_TOM, 84)
    d.note(0.01, 0.5, SLAP, 70)
    for i, name in enumerate(["G3", "E3", "C3"]):
        p.note(0.1 + i * 0.12, 0.4, n(name), 76 - i * 4)
    return s


def levelup():
    """The school levels up: vibraphone and celesta run up a C major chord, the piano lands on it,
    a soft cymbal swell."""
    s = _song()
    v = s.part("Vibraphone", 0, 11, volume=118, reverb=40)
    c = s.part("Celesta", 1, 8, volume=105, reverb=46)
    p = s.part("Piano", 2, 0, volume=105, reverb=40)
    d = s.part("Drums", 9, 40, bank=128, volume=100, reverb=40)
    for i, name in enumerate(["C4", "E4", "G4", "C5", "E5"]):
        v.note(i * 0.16, 1.6 - i * 0.16, n(name), 70 + i * 4)
    c.note(0.8, 1.6, n("G5"), 70)
    c.note(0.96, 1.4, n("E5"), 64)
    p.chord(0.8, 2.2, [n("C3"), n("G3"), n("E4"), n("G4"), n("C5")], 72, roll=0.03)
    d.note(0.8, 2, CRASH, 50)
    return s


# name (as in the game's Sfx enum): (score, seconds kept, peak in dBFS)
SOUNDS = {
    "Tap": (tap, 0.35, -18),
    "Select": (select, 0.6, -18),
    "Page": (page, 0.9, -16),
    "Pop": (pop, 0.8, -15),
    "Chime": (chime, 1.2, -15),
    "Bell": (bell, 1.9, -12),
    "Coin": (coin, 1.0, -12),
    "Error": (error, 0.9, -12),
    "Build": (build, 1.0, -10),
    "Demolish": (demolish, 1.0, -10),
    "LevelUp": (levelup, 2.8, -8),
}
