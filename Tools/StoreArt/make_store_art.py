"""Builds the Steam store images from captures of the game.

1. Capture the title art and store screenshots with the release build (see README, "Releasing"):
     BackToSchoolInc.exe -keyart <captures> -supersize 3 -screen-width 1440 -screen-height 810 -screen-fullscreen 0
     BackToSchoolInc.exe -shots <captures>/shots -big -supersize 2 -screen-width 1440 -screen-height 810 -screen-fullscreen 0
2. python Tools/StoreArt/make_store_art.py <captures> Marketing/Steam

Writes the capsules (header, small, main, vertical, library capsule, library hero, library logo)
and 1920x1080 screenshots. The logo is drawn with the game's own font, Gochi Hand, in the title
screen's style: blue "Back To", white "School" and blue "Inc.", inked outline, offset ink shadow.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, '..', '..', 'Assets', 'UI', 'Resources', 'Fonts')
INK = (30, 58, 109, 255)
PAPER = (251, 253, 255, 255)
BLUE = (90, 155, 234, 255)
REF_W = 1440   # the captures are the 1440x810 title screen, scaled up
TOWER_X = 0.694   # the school's flagpole, as fractions of the title art's width and height
FLAG_TOP = 0.142


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def crop_ratio(img, ratio, cx=0.5, cy=0.5):
    """Largest crop of the given width/height ratio around a centre point (fractions of the image)."""
    w, h = img.size
    if w / h > ratio:
        cw, ch = int(h * ratio), h
    else:
        cw, ch = w, int(w / ratio)
    x = min(max(int(cx * w - cw / 2), 0), w - cw)
    y = min(max(int(cy * h - ch / 2), 0), h - ch)
    return img.crop((x, y, x + cw, y + ch))


def fit(img, size):
    return crop_ratio(img, size[0] / size[1]).resize(size, Image.LANCZOS)


# The title screen's logo words, as in Screens.uss (.logo-word and its variants): text, colour,
# font size, left margin, top margin (negative: each line tucks into the next one's line box),
# outline width and shadow offset.
LOGO = (
    ('Back To', BLUE, 80, 14, 0, 3, (4, 5)),
    ('School', PAPER, 144, 0, -43, 4, (6, 7)),
    ('Inc.', BLUE, 124, 118, -64, 4, (6, 7)),
)


def logo(height):
    """The title screen's logo on a transparent canvas, about `height` pixels tall: 'Back To' over
    a big 'School', with 'Inc.' staggered below it."""
    u = height / 230   # the words stand about 230 px tall on the 1440x810 title screen
    img = Image.new('RGBA', (int(560 * u), int(320 * u)), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    y = 0.0
    for word, color, size, left, top, outline, shadow in LOGO:
        f = font('GochiHand-Regular.ttf', max(8, round(size * u)))
        stroke = max(1, round(outline * u))
        sx, sy = max(1, round(shadow[0] * u)), max(1, round(shadow[1] * u))
        y += top * u
        x = left * u
        d.text((x + sx + stroke, y + sy + stroke), word, font=f, fill=INK, stroke_width=stroke, stroke_fill=INK)
        d.text((x + stroke, y + stroke), word, font=f, fill=color, stroke_width=stroke, stroke_fill=INK)
        ascent, descent = f.getmetrics()
        y += ascent + descent
    img = img.crop(img.getbbox())
    return img.rotate(3, resample=Image.BICUBIC, expand=True)


def grid_look(backdrop, s):
    """The title backdrop's graph paper, read from the capture: paper, line and crossing colours,
    the square size, and the half-width of the lines, which are centred on multiples of the square."""
    step = round(32 * s)
    px = backdrop.load()
    paper, line, cross = px[step // 2, step // 2], px[step // 2, step], px[step, step]
    half = sum(1 for y in range(step // 2, step + step // 2) if px[step // 2, y] == line) // 2
    return paper, line, cross, step, half


def continue_grid(look, width, height, x0, y0):
    """More of the same graph paper: pixel (x, y) here is (x0 + x, y0 + y) on the backdrop."""
    paper, line, cross, step, half = look
    def on(v): return (v + half) % step < 2 * half
    plain = Image.new('RGBA', (width, 1), paper)
    ruled = Image.new('RGBA', (width, 1), line)
    for x in range(width):
        if on(x0 + x):
            plain.putpixel((x, 0), line)
            ruled.putpixel((x, 0), cross)
    img = Image.new('RGBA', (width, height))
    for y in range(height):
        img.paste(ruled if on(y0 + y) else plain, (0, y))
    return img


def tall(backdrop, look, size, block):
    """A tall capsule: the school under a taller sky, with the logo block centred in the clear paper
    above the flag. The sky grows just enough for the block to fit."""
    w, h = backdrop.size
    top = margin = int(size[1] * 0.03)
    room = top + block.height + margin   # the flag starts below this line
    sky = max(0, math.ceil((room * h - FLAG_TOP * h * size[1]) / (size[1] - room)))
    ch = h + sky
    cw = round(ch * size[0] / size[1])
    if cw > w:
        raise SystemExit(f'the backdrop is too narrow for a {size} capsule with this logo')
    x0 = min(max(round(TOWER_X * w - cw / 2), 0), w - cw)
    img = Image.new('RGBA', (cw, ch))
    img.paste(continue_grid(look, cw, sky, x0, -sky), (0, 0))
    img.paste(backdrop.crop((x0, 0, x0 + cw, h)), (0, sky))
    img = img.resize(size, Image.LANCZOS)
    img.alpha_composite(block, ((size[0] - block.width) // 2, top))
    return img


def wide(backdrop, look, size, art_x0, logo_frac, art_h=1.0):
    """A wide capsule: the school (from `art_x0` of the title art's width) on the right, `art_h` of the
    height tall, more of the same graph paper on the left and above, and the logo in that clear paper,
    at most `logo_frac` of the height tall. The art's left edge fades into the paper. It is built at the
    art's resolution and scaled down as one picture, so paper and art match. Only art and the game's
    name: Valve rejects capsules with any other text or logos."""
    w, h = backdrop.size
    y0, y1 = int(0.03 * h), int(0.90 * h)   # sky above the flag down to the children on the path
    art = backdrop.crop((int(art_x0 * w), y0, w, y1))
    scale = size[1] * art_h / art.height
    cw, ch = round(size[0] / scale), round(size[1] / scale)   # the canvas at the art's resolution
    pad, top = max(0, cw - art.width), ch - art.height
    src = continue_grid(look, cw, ch, int(art_x0 * w) - pad, y0 - top)
    fade = int(art.width * 0.12)
    mask = Image.new('L', art.size, 255)
    for x in range(fade):
        mask.paste(round(255 * (x / fade) ** 1.5), (x, 0, x + 1, art.height))
    src.paste(art, (pad, top), mask)
    img = src.resize(size, Image.LANCZOS)
    margin = round(size[1] * 0.07)
    room = round((pad + fade * 0.4) * scale) - margin   # clear paper left of the art
    mark = logo(round(size[1] * logo_frac))
    if mark.width > room:
        mark = mark.resize((room, round(mark.height * room / mark.width)), Image.LANCZOS)
    img.alpha_composite(mark, (margin, (size[1] - mark.height) // 2))
    return img


def small(backdrop, look, size):
    """The small capsule: the logo nearly filling it, on the title's graph paper at the usual square size."""
    k = 6   # build at 6x, like the other capsules' scale-down, so the grid squares match
    paper = continue_grid(look, size[0] * k, size[1] * k, 0, 0).resize(size, Image.LANCZOS)
    mark = logo(round(size[1] * 0.9))
    if mark.width > size[0] * 0.94:
        mark = mark.resize((round(size[0] * 0.94), round(mark.height * size[0] * 0.94 / mark.width)), Image.LANCZOS)
    paper.alpha_composite(mark, ((size[0] - mark.width) // 2, (size[1] - mark.height) // 2))
    return paper


def main(captures, out):
    os.makedirs(out, exist_ok=True)
    backdrop = Image.open(os.path.join(captures, 'backdrop.png')).convert('RGBA')
    s = backdrop.width / REF_W

    def save(img, name):
        img.convert('RGB').save(os.path.join(out, name), optimize=True)
        print(name, img.size)

    look = grid_look(backdrop, s)
    # Wide formats: school on the right, big logo on the left.
    save(wide(backdrop, look, (1232, 706), 0.465, 0.62, art_h=0.82), 'main_capsule_1232x706.png')
    save(wide(backdrop, look, (920, 430), 0.465, 0.74), 'header_capsule_920x430.png')
    save(wide(backdrop, look, (920, 430), 0.465, 0.74), 'library_header_920x430.png')
    save(small(backdrop, look, (462, 174)), 'small_capsule_462x174.png')

    # Tall formats: the school with the logo in the sky above it.
    save(tall(backdrop, look, (600, 900), logo(230)), 'library_capsule_600x900.png')
    save(tall(backdrop, look, (748, 896), logo(250)), 'vertical_capsule_748x896.png')

    # Library hero: art only (Steam puts the logo on top), sky and school.
    hero = crop_ratio(backdrop, 3840 / 1240, cy=0.4)
    save(hero.resize((3840, 1240), Image.LANCZOS), 'library_hero_3840x1240.png')

    # Library logo: transparent.
    mark = logo(560)
    canvas = Image.new('RGBA', (1280, 720), (0, 0, 0, 0))
    canvas.alpha_composite(mark, ((1280 - mark.width) // 2, (720 - mark.height) // 2))
    canvas.save(os.path.join(out, 'library_logo_1280x720.png'))
    print('library_logo_1280x720.png', canvas.size)

    # Screenshots at 1920x1080, leaving out the ones that only check the game: the settings card,
    # the title screen a second time, and the practice field, which is mostly empty grass.
    shots = os.path.join(captures, 'shots')
    if os.path.isdir(shots):
        os.makedirs(os.path.join(out, 'screenshots'), exist_ok=True)
        for name in sorted(os.listdir(shots)):
            if name.endswith('.png') and not any(skip in name for skip in ('settings', 'again', 'PracticeField')):
                img = Image.open(os.path.join(shots, name)).convert('RGB')
                fit(img, (1920, 1080)).save(os.path.join(out, 'screenshots', name), optimize=True)
                print('screenshots/' + name)


if __name__ == '__main__':
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
