"""Renders the game's music with real sampled instruments.

    python Tools/Music/render.py [campus] [title] [trailer] [sfx]      (default: all four)
    python Tools/Music/render.py --stems trailer                       (each part's level)

Each song (songs.py) is written as MIDI and played through FluidSynth with the GeneralUser GS
soundfont, both in Tools/Music/vendor (ignored by git; see the README's "Music and sounds").
The loops' reverb tails are folded back onto their starts so they repeat seamlessly, then each
song gets one constant gain to its target loudness (EBU R128) and a peak check:
  - campus and title -> Assets/Audio/Resources/Music/<Name>.ogg (Vorbis), -18 LUFS
  - the sound effects (sfx.py) -> Assets/Audio/Resources/Sfx/<Name>.wav, each at its own peak
  - trailer          -> Tools/Music/out/trailer.wav, -16 LUFS, for Tools/Trailer/make_trailer.py
It prints each one's loudness and where its energy sits, so "squeaky" can be checked by numbers.
"""
import os
import re
import subprocess
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import songs  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(HERE))
VENDOR = os.path.join(HERE, "vendor")
FLUIDSYNTH = os.path.join(VENDOR, "fluidsynth", "fluidsynth-v2.6.1-win10-x64-cpp11", "bin", "fluidsynth.exe")
SOUNDFONT = os.path.join(VENDOR, "GeneralUser-GS.sf2")
OUT = os.path.join(HERE, "out")
GAME_MUSIC = os.path.join(ROOT, "Assets", "Audio", "Resources", "Music")
GAME_SFX = os.path.join(ROOT, "Assets", "Audio", "Resources", "Sfx")
RATE = 48000
TAIL = 4.0   # seconds of reverb and ringing kept after the last bar (folded into loops)
TRAILER_TAIL = 2.5

SONGS = {
    "campus": (songs.campus, "Campus", True, -18.0),
    "title": (songs.title, "Title", True, -18.0),
    "trailer": (songs.trailer, None, False, -16.0),
}
# The title is solo piano and strings, mellow by nature: a little more top so it isn't muffled.
EXTRA_EQ = {"title": ",highshelf=f=2500:t=q:w=0.7:g=4"}


def read_wav(path):
    with wave.open(path) as w:
        ch, width, frames = w.getnchannels(), w.getsampwidth(), w.readframes(w.getnframes())
    if width == 4:
        data = np.frombuffer(frames, dtype=np.float32)
    else:
        data = np.frombuffer(frames, dtype=np.int16).astype(np.float32) / 32768
    return data.reshape(-1, ch).astype(np.float64)


def read_float_wav(path):
    """FluidSynth writes WAVE_FORMAT_IEEE_FLOAT, which the wave module won't open: parse it."""
    raw = open(path, "rb").read()
    i = 12
    channels, data = 2, None
    while i < len(raw):
        cid, size = raw[i:i + 4], int.from_bytes(raw[i + 4:i + 8], "little")
        if cid == b"fmt ":
            channels = int.from_bytes(raw[i + 10:i + 12], "little")
        elif cid == b"data":
            data = np.frombuffer(raw[i + 8:i + 8 + size], dtype=np.float32)
        i += 8 + size + (size & 1)
    return data.reshape(-1, channels).astype(np.float64)


def write_wav(path, x):
    pcm = (np.clip(x, -1, 1) * 32767).round().astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(x.shape[1])
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())


def loudness(path):
    """Integrated loudness (LUFS) and true peak (dBTP) from ffmpeg's EBU R128 meter."""
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", path, "-af", "ebur128=peak=true", "-f", "null", "-"],
                       capture_output=True, text=True)
    text = r.stderr[r.stderr.rfind("Summary:"):]
    lufs = float(re.search(r"I:\s+(-?[\d.]+) LUFS", text).group(1))
    peak = float(re.search(r"Peak:\s+(-?[\d.]+|-inf) dBFS", text).group(1))
    return lufs, peak


def describe(name, x):
    mono = x.mean(axis=1)
    n = 8192
    frames = [mono[i:i + n] * np.hanning(n) for i in range(0, len(mono) - n, n // 2)]
    spec = np.mean([np.abs(np.fft.rfft(f)) ** 2 for f in frames], axis=0)
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    total = spec.sum()
    bands = [(20, 150), (150, 400), (400, 1000), (1000, 2000), (2000, 4000), (4000, 8000), (8000, 20000)]
    parts = []
    for lo, hi in bands:
        m = (freqs >= lo) & (freqs < hi)
        parts.append(f"{lo}-{hi}: {100 * spec[m].sum() / total:.0f}%")
    centroid = (freqs * spec).sum() / total
    side = (x[:, 0] - x[:, 1]) / 2
    width = np.sqrt((side ** 2).mean()) / max(1e-9, np.sqrt((mono ** 2).mean()))
    print(f"  {name}: {len(x) / RATE:.1f} s, centroid {centroid:.0f} Hz, stereo width {width:.2f}")
    print("    energy " + ", ".join(parts))
    octaves = []
    for centre in (62.5, 125, 250, 500, 1000, 2000, 4000, 8000, 16000):
        m = (freqs >= centre / 2 ** 0.5) & (freqs < centre * 2 ** 0.5)
        octaves.append((centre, 10 * np.log10(spec[m].sum() + 1e-20)))
    top = max(level for _, level in octaves)
    print("    octaves (dB) " + "  ".join(f"{int(c)}:{level - top:+.0f}" for c, level in octaves))


# The master EQ: rumble out, a little less of the boxy 450 Hz the vibes and piano pile up, and a
# soft lift above 4.5 kHz for the brushes and the air around the instruments.
EQ = "highpass=f=28:p=2,equalizer=f=450:t=q:w=0.9:g=-2.5,highshelf=f=4500:t=q:w=0.7:g=2.5"


def master_eq(x, loop, stem, extra=""):
    """Runs the EQ through ffmpeg. A loop is filtered as two copies back to back and the second
    kept, so the filters are already settled at the loop point and the seam stays clean."""
    src, dst = stem + "_in.wav", stem + "_out.wav"
    write_float_wav(src, np.concatenate([x, x]) if loop else x)
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", src, "-af", EQ + extra, "-c:a", "pcm_f32le", dst], check=True)
    y = read_float_wav(dst)
    for f in (src, dst):
        os.remove(f)
    return y[len(x):] if loop else y[:len(x)]


def write_float_wav(path, x):
    data = x.astype(np.float32).tobytes()
    ch = x.shape[1]
    with open(path, "wb") as f:
        f.write(b"RIFF" + (36 + len(data)).to_bytes(4, "little") + b"WAVEfmt ")
        f.write((16).to_bytes(4, "little") + (3).to_bytes(2, "little") + ch.to_bytes(2, "little"))
        f.write(RATE.to_bytes(4, "little") + (RATE * ch * 4).to_bytes(4, "little"))
        f.write((ch * 4).to_bytes(2, "little") + (32).to_bytes(2, "little"))
        f.write(b"data" + len(data).to_bytes(4, "little") + data)


def render(key):
    build, game_name, loop, target = SONGS[key]
    song, beats = build()
    length = song.seconds(beats)
    # A silent controller after the tail, so the MIDI (and FluidSynth's render) runs that long.
    end = song.part("end", 15, 0, volume=0)
    tail = TAIL if loop else TRAILER_TAIL
    end_beat = beats + tail / (60.0 / song.tempos[-1][1])
    end.cc(end_beat, 110, 0)
    os.makedirs(OUT, exist_ok=True)
    mid, raw_wav = os.path.join(OUT, key + ".mid"), os.path.join(OUT, key + "_raw.wav")
    song.write(mid)
    subprocess.run([FLUIDSYNTH, "-ni", "-q", "-F", raw_wav, "-T", "wav", "-O", "float", "-r", str(RATE), "-g", "0.5",
                    "-R", "1", "-C", "1",
                    "-o", "synth.reverb.room-size=0.62", "-o", "synth.reverb.damp=0.4",
                    "-o", "synth.reverb.width=0.9", "-o", "synth.reverb.level=0.7",
                    "-o", "synth.chorus.level=0.6", "-o", "synth.chorus.depth=6",
                    SOUNDFONT, mid], check=True)
    x = read_float_wav(raw_wav)
    total = int(round(length * RATE))
    if loop:
        # Fold everything after the loop point onto its start: the end's tail flows into the
        # beginning, as it will when the clip repeats.
        body = x[:total].copy()
        tail = x[total:]
        body[:len(tail)] += tail[:total]
        x = body
    else:
        x = x[:total + int(tail * RATE)]
        fade = int(1.5 * RATE)
        x[-fade:] *= np.linspace(1, 0, fade)[:, None] ** 2
    x -= x.mean(axis=0)
    x = master_eq(x, loop, os.path.join(OUT, key + "_eq"), EXTRA_EQ.get(key, ""))
    tmp = os.path.join(OUT, key + "_level.wav")
    write_wav(tmp, x / max(1e-9, np.abs(x).max()) * 0.5)
    lufs, _ = loudness(tmp)
    gain = 10 ** ((target - lufs) / 20) * 0.5 / max(1e-9, np.abs(x).max())
    x = x * gain
    peak = np.abs(x).max()
    if peak > 0.89:   # about -1 dBFS: a soft knee above 0.7 instead of hard clipping
        over = np.abs(x) > 0.7
        x[over] = np.sign(x[over]) * (0.7 + 0.19 * np.tanh((np.abs(x[over]) - 0.7) / 0.19))
    final = os.path.join(OUT, key + ".wav")
    write_wav(final, x)
    lufs, tp = loudness(final)
    print(f"{key}: {lufs:.1f} LUFS, peak {tp:.1f} dBFS" + (" (loop)" if loop else ""))
    describe(key, x)
    if game_name:
        os.makedirs(GAME_MUSIC, exist_ok=True)
        ogg = os.path.join(GAME_MUSIC, game_name + ".ogg")
        subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", final, "-c:a", "libvorbis", "-q:a", "7", ogg], check=True)
        print(f"  -> {os.path.relpath(ogg, ROOT)} ({os.path.getsize(ogg) / 1e6:.1f} MB)")
    for f in (raw_wav, tmp):
        os.remove(f)
    return final


def render_sfx():
    """Each sound effect: rendered, trimmed to its length with a short fade, set to its peak."""
    import sfx
    os.makedirs(GAME_SFX, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    for name, (score, seconds, peak_db) in sfx.SOUNDS.items():
        song = score()
        end = song.part("end", 15, 0, volume=0)
        end.cc((seconds + 0.5) * song.tempos[0][1] / 60.0, 110, 0)
        mid, raw_wav = os.path.join(OUT, "sfx.mid"), os.path.join(OUT, "sfx_raw.wav")
        song.write(mid)
        subprocess.run([FLUIDSYNTH, "-ni", "-q", "-F", raw_wav, "-T", "wav", "-O", "float", "-r", str(RATE), "-g", "0.5",
                        "-R", "1", "-C", "0", "-o", "synth.reverb.room-size=0.4", "-o", "synth.reverb.level=0.5",
                        SOUNDFONT, mid], check=True)
        x = read_float_wav(raw_wav)[: int(seconds * RATE)]
        x -= x.mean(axis=0)
        fade = int(0.04 * RATE)
        x[-fade:] *= np.linspace(1, 0, fade)[:, None]
        x *= 10 ** (peak_db / 20) / max(1e-9, np.abs(x).max())
        out = os.path.join(GAME_SFX, name + ".wav")
        write_wav(out, x)
        mono = x.mean(axis=1)
        spec = np.abs(np.fft.rfft(mono)) ** 2
        freqs = np.fft.rfftfreq(len(mono), 1 / RATE)
        print(f"  {name:<9} {seconds:.2f} s, peak {peak_db} dBFS, centroid {(freqs * spec).sum() / spec.sum():5.0f} Hz")
        os.remove(mid)
        os.remove(raw_wav)


def stems(key):
    """Each part soloed: its loudness, and its share of the full mix's loudness."""
    build = SONGS[key][0]
    names = [p.name for p in build()[0].parts]
    os.makedirs(OUT, exist_ok=True)
    rows = []
    for name in names:
        song, beats = build()
        song.parts = [p for p in song.parts if p.name == name]
        mid, wav = os.path.join(OUT, "stem.mid"), os.path.join(OUT, "stem.wav")
        song.write(mid)
        subprocess.run([FLUIDSYNTH, "-ni", "-q", "-F", wav, "-T", "wav", "-O", "float", "-r", str(RATE), "-g", "0.5",
                        "-R", "1", "-C", "1", "-o", "synth.reverb.level=0.7", SOUNDFONT, mid], check=True)
        x = read_float_wav(wav)
        if not len(x) or np.abs(x).max() < 1e-6:
            continue
        pcm = os.path.join(OUT, "stem16.wav")
        write_wav(pcm, x)
        lufs, peak = loudness(pcm)
        mono = x.mean(axis=1)
        spec = np.abs(np.fft.rfft(mono[: 1 << 20] if len(mono) > 1 << 20 else mono)) ** 2
        freqs = np.fft.rfftfreq(min(len(mono), 1 << 20), 1 / RATE)
        centroid = (freqs * spec).sum() / spec.sum()
        rows.append((lufs, name, peak, centroid))
    for f in ("stem.mid", "stem.wav", "stem16.wav"):
        os.remove(os.path.join(OUT, f))
    print(f"{key} stems (loudness on their own; the melody should lead):")
    for lufs, name, peak, centroid in sorted(rows, reverse=True):
        print(f"  {name:<14} {lufs:6.1f} LUFS  peak {peak:6.1f}  centroid {centroid:5.0f} Hz")


if __name__ == "__main__":
    args = sys.argv[1:]
    if args and args[0] == "--stems":
        for key in args[1:] or ["trailer"]:
            stems(key)
    else:
        for key in args or ["campus", "title", "trailer", "sfx"]:
            if key == "sfx":
                render_sfx()
            else:
                render(key)
