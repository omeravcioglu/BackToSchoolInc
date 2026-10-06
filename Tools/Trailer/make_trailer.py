"""Builds the Steam trailer: 1080p at 60 fps, cut on the beat of its own music.

    python Tools/Trailer/make_trailer.py record <BackToSchoolInc.exe> <capture>
    python Tools/Trailer/make_trailer.py build <capture> Marketing/Steam/trailer.mp4

record renders the trailer music (Tools/Music/render.py), writes <capture>/cuts.txt with each
shot's length in frames (from the music's bars, so every cut lands on a beat), and runs the release
build with "-trailer", which plays the tour on a fixed 1/60 s clock and saves every frame. Keep the
window visible while it records (a minute or two).

build draws a caption card for each group of shots in the notebook style (the game's own fonts),
cuts the shots together with a short flash of paper white on each cut, lays the music under them
and encodes H.264 1080p60 with AAC audio. Needs Pillow and ffmpeg on the PATH.
"""
import os
import subprocess
import sys
import wave

from PIL import Image, ImageDraw, ImageFont

FPS = 60
W, H = 1920, 1080
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONTS = os.path.join(ROOT, "Assets", "UI", "Resources", "Fonts")
MUSIC = os.path.join(ROOT, "Tools", "Music")
INK = (31, 63, 143)          # the HUD's ballpoint blue
PAPER = (253, 254, 255)
SHADOW = (166, 196, 240)

# Caption per shot (shots in a row with the same caption share one card). None: no caption.
CAPTIONS = [
    ("title", None),
    ("young_school", "Found a tiny boarding school"),
    ("follow", "Every student is someone"),
    ("person_card", "Every student is someone"),
    ("inbox", "Every letter has a cost"),
    ("build", "Build the campus"),
    ("life_", "Watch school life happen"),
    ("page_Students", "Run every corner of it"),
    ("page_Staff", "Run every corner of it"),
    ("page_Money", "Run every corner of it"),
    ("page_Ranks", "Climb the rankings"),
    ("level_up", "Grow into an institution"),
    ("pull_back", "Make it the best in the country"),
    ("end_card", "Wishlist now on Steam"),
]


def caption_for(shot):
    for prefix, text in CAPTIONS:
        if shot == prefix or (prefix.endswith("_") and shot.startswith(prefix)):
            return text
    return None


def card(text, path, end_card=False):
    """A sticky-note caption: paper card, ink border and text, a soft blue offset shadow."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(os.path.join(FONTS, "PlaypenSans-ExtraBold.ttf"), 64 if end_card else 56)
    x0, y0, x1, y1 = d.textbbox((0, 0), text, font=font)
    tw, th = x1 - x0, y1 - y0
    pad_x, pad_y = 44, 26
    bw, bh = tw + 2 * pad_x, th + 2 * pad_y
    if end_card:
        bx, by = 120, 700                  # under the logo and tagline, left of the school
    else:
        bx, by = (W - bw) // 2, H - bh - 150   # clear of the game's toasts at the very bottom
    d.rounded_rectangle((bx + 10, by + 10, bx + bw + 10, by + bh + 10), 22, fill=SHADOW + (255,))
    d.rounded_rectangle((bx, by, bx + bw, by + bh), 22, fill=PAPER + (255,), outline=INK + (255,), width=5)
    d.text((bx + pad_x - x0, by + pad_y - y0), text, font=font, fill=INK + (255,))
    img.save(path)


def wav_seconds(path):
    with wave.open(path) as w:
        return w.getnframes() / w.getframerate()


def run(args, **kw):
    print(" ".join(str(a) for a in args[:6]), "...")
    subprocess.run(args, check=True, **kw)


def music():
    """Renders the trailer music and returns its path."""
    run([sys.executable, os.path.join(MUSIC, "render.py"), "trailer"])
    return os.path.join(MUSIC, "out", "trailer.wav")


def cut_frames(total_seconds):
    """Each shot's length in frames: boundaries rounded to the nearest frame (so rounding never
    adds up), the end card held until the music has rung out."""
    sys.path.insert(0, MUSIC)
    import songs
    cuts = songs.trailer_cuts()
    starts = [round(start * FPS) for _, start, _ in cuts] + [round(total_seconds * FPS)]
    return [(name, starts[i + 1] - starts[i]) for i, (name, _, _) in enumerate(cuts)]


def record(exe, capture):
    os.makedirs(capture, exist_ok=True)
    wav = music()
    shots = cut_frames(wav_seconds(wav))
    with open(os.path.join(capture, "cuts.txt"), "w") as f:
        for name, frames in shots:
            f.write(f"{name} {frames}\n")
    print(f"{len(shots)} shots, {sum(f for _, f in shots)} frames ({sum(f for _, f in shots) / FPS:.1f} s)")
    run([exe, "-trailer", capture, "-screen-width", str(W), "-screen-height", str(H), "-screen-fullscreen", "0",
         "-logFile", os.path.join(capture, "player.log")])
    print(open(os.path.join(capture, "report.txt")).read())


def build(capture, out):
    shots = []
    with open(os.path.join(capture, "timeline.txt")) as f:
        for line in f:
            name, first, count = line.split()
            shots.append((name, int(first), int(count)))
    wav = os.path.join(MUSIC, "out", "trailer.wav")
    if not os.path.exists(wav):
        wav = music()

    work = os.path.join(capture, "build")
    os.makedirs(work, exist_ok=True)
    # One clip per shot: frames, caption (fading in at the start of its group, out at the end),
    # and a short flash from paper white at each cut, on the beat.
    clips = []
    for i, (name, first, count) in enumerate(shots):
        text = caption_for(name)
        prev = caption_for(shots[i - 1][0]) if i > 0 else None
        nxt = caption_for(shots[i + 1][0]) if i + 1 < len(shots) else None
        dur = count / FPS
        clip = os.path.join(work, f"{i:02d}_{name}.mp4")
        inputs = ["-framerate", str(FPS), "-start_number", str(first), "-i", os.path.join(capture, "frames", "f%05d.jpg")]
        chain = f"[0:v]trim=end_frame={count},setpts=PTS-STARTPTS,format=yuv420p"
        if i == 0:
            chain += ",fade=t=in:st=0:d=0.6:color=white"
        else:
            chain += ",fade=t=in:st=0:d=0.18:color=white"
        if i == len(shots) - 1:
            chain += f",fade=t=out:st={dur - 1.5:.2f}:d=1.5:color=white"
        chain += "[base]"
        filters = [chain]
        if text:
            png = os.path.join(work, f"cap_{i:02d}.png")
            card(text, png, end_card=(name == "end_card"))
            inputs += ["-loop", "1", "-framerate", str(FPS), "-i", png]
            fin = 0.3 if text != prev else 0.0
            fout = 0.3 if text != nxt else 0.0
            cap = "[1:v]format=rgba"
            if fin:
                cap += f",fade=t=in:st={1.2 if name == 'end_card' else 0.15}:d={fin}:alpha=1"
            if fout and name != "end_card":
                cap += f",fade=t=out:st={dur - fout - 0.05:.2f}:d={fout}:alpha=1"
            cap += "[cap]"
            filters += [cap, "[base][cap]overlay=0:0:shortest=1,format=yuv420p[v]"]
        else:
            filters[0] = filters[0].replace("[base]", "[v]")
        run(["ffmpeg", "-loglevel", "error", "-y", *inputs, "-filter_complex", ";".join(filters), "-map", "[v]",
             "-frames:v", str(count), "-r", str(FPS), "-c:v", "libx264", "-preset", "slow", "-crf", "16",
             "-profile:v", "high", "-level", "4.2", "-g", "120", "-pix_fmt", "yuv420p", clip])
        clips.append(clip)

    with open(os.path.join(work, "clips.txt"), "w") as f:
        for c in clips:
            f.write(f"file '{os.path.abspath(c)}'\n")
    video = os.path.join(work, "video.mp4")
    run(["ffmpeg", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", os.path.join(work, "clips.txt"), "-c", "copy", video])

    total = sum(c for _, _, c in shots) / FPS
    run(["ffmpeg", "-loglevel", "error", "-y", "-i", video, "-i", wav, "-map", "0:v", "-map", "1:a", "-c:v", "copy",
         "-c:a", "aac", "-b:a", "320k", "-ar", "48000", "-t", f"{total:.3f}", "-movflags", "+faststart", out])
    print(f"{out}: {total:.1f} s at {FPS} fps, music {wav_seconds(wav):.1f} s")


if __name__ == "__main__":
    if len(sys.argv) == 4 and sys.argv[1] == "record":
        record(sys.argv[2], sys.argv[3])
    elif len(sys.argv) == 4 and sys.argv[1] == "build":
        build(sys.argv[2], sys.argv[3])
    else:
        print(__doc__)
        sys.exit(1)
