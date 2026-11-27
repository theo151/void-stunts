"""Generates every texture Void Stunts ships with.

Run:  python3 Tools/gen_textures.py   (numpy, scipy, pillow)
All surface textures are periodic (built in the frequency domain), and the world shader
breaks the repetition with hex-tiling + world-space macro variation, so no tile grid is ever visible.
Packing:  *_a.png albedo (sRGB) · *_n.png normal (linear, OpenGL +Y) · *_m.png mask (linear: R roughness, G AO, B height)
"""
import os, math, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, 'Assets', 'Resources', 'Textures')
FONTS = os.path.join(ROOT, 'Assets', 'Resources', 'Fonts')
os.makedirs(TEX, exist_ok=True)
rng = np.random.default_rng(20261126)

# ---------- periodic noise ----------
def fbm(n, beta=2.0, lo=1.0, hi=None, seed=None):
    """Periodic 1/f^beta noise in [0,1]; lo/hi limit the frequency band (cycles per tile)."""
    r = np.random.default_rng(seed) if seed is not None else rng
    w = r.standard_normal((n, n))
    F = np.fft.fft2(w)
    fy = np.fft.fftfreq(n)[:, None] * n; fx = np.fft.fftfreq(n)[None, :] * n
    f = np.sqrt(fx * fx + fy * fy); f[0, 0] = 1
    amp = 1.0 / f ** (beta / 2)
    amp[f < lo] = 0
    if hi: amp *= np.exp(-(f / hi) ** 2)
    out = np.real(np.fft.ifft2(F * amp))
    out -= out.min(); out /= max(1e-9, out.max())
    return out

def voronoi(n, count, seed=1):
    """Periodic voronoi: returns (cell id map, distance to nearest, distance edge (f2-f1))."""
    r = np.random.default_rng(seed)
    pts = r.random((count, 2)) * n
    tiled = np.concatenate([pts + np.array([dx, dy]) * n for dx in (-1, 0, 1) for dy in (-1, 0, 1)])
    ids = np.tile(np.arange(count), 9)
    from scipy.spatial import cKDTree
    tree = cKDTree(tiled)
    yy, xx = np.mgrid[0:n, 0:n]
    q = np.stack([xx.ravel() + .5, yy.ravel() + .5], 1)
    d, i = tree.query(q, k=2)
    cid = ids[i[:, 0]].reshape(n, n)
    return cid, d[:, 0].reshape(n, n), (d[:, 1] - d[:, 0]).reshape(n, n)

def normal_from_height(h, strength):
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    nz = np.ones_like(h)
    nx, ny = -gx, gy  # OpenGL convention: +Y up in texture space (image rows go down)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / l, ny / l, nz / l], -1) * .5 + .5

def save_rgb(a, name, srgb=True):
    a = np.clip(a, 0, 1)
    Image.fromarray((a * 255 + .5).astype(np.uint8), 'RGB').save(os.path.join(TEX, name), optimize=True)

def save_set(name, albedo, height, rough, ao, nstr):
    save_rgb(albedo, name + '_a.png')
    save_rgb(normal_from_height(height, nstr), name + '_n.png')
    save_rgb(np.stack([rough, ao, height], -1), name + '_m.png')
    print('wrote', name)

def tint(gray, col):
    return gray[..., None] * np.array(col)[None, None, :]

# ---------- concrete: cast-in-place, mottled, pored ----------
def concrete(n=1024):
    m1 = fbm(n, 2.2, 1, 40); m2 = fbm(n, 1.6, 6, 200); speck = fbm(n, .2, 120)
    pores = np.zeros((n, n))
    for _ in range(2600):
        x, y, r = rng.integers(0, n), rng.integers(0, n), rng.uniform(.6, 2.6)
        rr = int(math.ceil(r)) + 1
        ys, xs = np.mgrid[-rr:rr + 1, -rr:rr + 1]
        mask = np.clip(r - np.sqrt(xs * xs + ys * ys), 0, 1)
        pores[(y + ys) % n, (x + xs) % n] = np.maximum(pores[(y + ys) % n, (x + xs) % n], mask)
    g = .6 + (m1 - .5) * .13 + (m2 - .5) * .07 + (speck - .5) * .05 - pores * .2
    warm = fbm(n, 2.4, 1, 20)
    alb = np.stack([g * (1.0 + (warm - .5) * .04), g * .99, g * (.96 - (warm - .5) * .04)], -1)
    h = m2 * .4 + speck * .15 - pores * .9 + m1 * .2
    rough = np.clip(.86 + (m2 - .5) * .1 + pores * .1, 0, 1)
    ao = np.clip(1 - pores * .6, 0, 1)
    save_set('concrete', alb, h, rough, ao, 3.0)

# ---------- asphalt: aggregate stones in binder, worn ----------
def asphalt(n=1024):
    cid, d1, edge = voronoi(n, 9000, seed=7)
    stone_b = np.random.default_rng(9).uniform(.05, .2, cid.max() + 1)[cid]
    stone_mask = ndimage.gaussian_filter(np.clip((edge - .8) / 2.0, 0, 1), .6, mode='wrap')
    wear = fbm(n, 2.4, 1, 30); fine = fbm(n, .3, 150)
    crack = fbm(n, 1.8, 3, 80); crack = np.clip(1 - np.abs(crack - .5) * 60, 0, 1) * (fbm(n, 2, 1, 10) > .6)
    g = .1 + stone_b * stone_mask * .55 + (wear - .5) * .07 + (fine - .5) * .03 - crack * .05
    alb = tint(g, (1.0, 1.0, 1.04))
    h = stone_mask * (.5 + stone_b) + fine * .1 - crack * .8
    rough = np.clip(.9 - stone_mask * .12 + (wear - .5) * .08, 0, 1)
    ao = np.clip(.55 + stone_mask * .45 - crack * .4, 0, 1)
    save_set('asphalt', alb, h, rough, ao, 5.0)

# ---------- moss / ground cover ----------
def moss(n=512):
    a = fbm(n, 1.4, 4, 160); b = fbm(n, 2.2, 1, 24); c = fbm(n, .4, 80)
    clump = np.clip((a - .35) * 2.2, 0, 1)
    pal_lo = np.array([.22, .28, .15]); pal_hi = np.array([.46, .54, .28]); dry = np.array([.52, .48, .32])
    col = pal_lo + (pal_hi - pal_lo) * clump[..., None]
    col = col * (1 - (b[..., None] > .7) * .3) + dry * ((b > .7) * (b - .7) * 2)[..., None]
    col *= (.9 + (c - .5) * .3)[..., None]
    h = clump * .8 + c * .3
    save_set('moss', col, h, np.clip(.95 - clump * .05, 0, 1), .6 + clump * .4, 4.0)

# ---------- rock: fractured sandstone (strata are added in the shader from world height) ----------
def rock(n=1024):
    cid, d1, edge = voronoi(n, 260, seed=21)
    frac = np.clip(edge / 9.0, 0, 1)
    a = fbm(n, 2.0, 1, 60); b = fbm(n, 1.2, 20, 300)
    g = .55 + (a - .5) * .25 + (b - .5) * .12 - (1 - frac) * .18
    alb = np.stack([g * 1.08, g * .86, g * .66], -1)
    h = frac * .6 + a * .5 + b * .2
    save_set('rock', alb, h, np.clip(.9 + (b - .5) * .1, 0, 1), np.clip(.4 + frac * .6, 0, 1), 6.0)

# ---------- sand with wind ripples ----------
def sand(n=1024):
    warp = fbm(n, 2.4, 1, 16)
    yy, xx = np.mgrid[0:n, 0:n] / n
    ripple = .5 + .5 * np.sin((yy * 38 + warp * 3.0 + xx * 3) * 2 * math.pi)
    grain = fbm(n, .1, 200)
    g = .72 + (ripple - .5) * .06 + (grain - .5) * .12 + (fbm(n, 2, 1, 20) - .5) * .1
    alb = np.stack([g * 1.06, g * .9, g * .72], -1)
    h = ripple * .7 + grain * .3
    save_set('sand', alb, h, np.full((n, n), .97), np.full((n, n), 1.0), 2.5)

# ---------- painted steel (structures, rails) ----------
def steel(n=512):
    a = fbm(n, 2.0, 1, 40); s = fbm(n, .5, 60); scr = fbm(n, 1.2, 10, 200)
    scr = np.clip((scr - .82) * 6, 0, 1)
    g = .5 + (a - .5) * .08 + (s - .5) * .04 + scr * .2
    alb = tint(g, (1, 1, 1))
    save_set('steel', alb, a * .3 + s * .2 - scr * .3, np.clip(.55 + (a - .5) * .2 - scr * .2, 0, 1), np.full((n, n), 1.0), 2.0)

# ---------- bark ----------
def bark(n=512):
    yy, xx = np.mgrid[0:n, 0:n] / n
    warp = fbm(n, 2.2, 1, 12)
    fib = .5 + .5 * np.sin((xx * 22 + warp * 2.4) * 2 * math.pi)
    fib = fib ** 3
    det = fbm(n, 1.0, 10, 160)
    g = .28 + fib * .14 + (det - .5) * .1
    alb = np.stack([g * 1.0, g * .86, g * .72], -1)
    save_set('bark', alb, fib * .8 + det * .3, np.full((n, n), .9), .5 + fib * .5, 7.0)

# ---------- macro noise (linear RGBA, four octaves) ----------
def noise_tex(n=512):
    ch = [fbm(n, 2.6, 1, 8, seed=31), fbm(n, 2.2, 2, 24, seed=32), fbm(n, 1.8, 6, 80, seed=33), fbm(n, 1.0, 20, 256, seed=34)]
    a = np.stack(ch, -1)
    Image.fromarray((a * 255).astype(np.uint8), 'RGBA').save(os.path.join(TEX, 'noise_m.png'))
    print('wrote noise')

# ---------- leaves: four leaf-cluster cards in a 2×2 atlas, with alpha ----------
def leaves(n=1024):
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    nrm = Image.new('RGB', (n, n), (128, 128, 255)); dn = ImageDraw.Draw(nrm)
    r = np.random.default_rng(77)
    pals = [[(70, 96, 52), (92, 122, 66), (58, 82, 46), (110, 138, 74)], [(84, 104, 58), (104, 128, 70), (66, 86, 50), (128, 146, 82)],
            [(60, 90, 62), (78, 112, 74), (48, 72, 52), (98, 130, 86)], [(150, 120, 62), (118, 100, 56), (96, 112, 60), (168, 138, 72)]]
    for q in range(4):
        ox, oy = (q % 2) * n // 2, (q // 2) * n // 2; S = n // 2
        # a stem fan, then leaves drawn back to front with a light top edge
        for k in range(7):
            a = -math.pi / 2 + (k - 3) * .32 + r.uniform(-.1, .1)
            d.line([(ox + S / 2, oy + S * .95), (ox + S / 2 + math.cos(a) * S * .42, oy + S * .95 + math.sin(a) * S * .8)], fill=(58, 48, 36, 255), width=3)
        for k in range(160):
            cx = ox + S / 2 + r.normal(0, S * .17); cy = oy + S * .45 + r.normal(0, S * .2)
            if not (ox + 20 < cx < ox + S - 20 and oy + 20 < cy < oy + S - 20): continue
            L = r.uniform(S * .06, S * .11); W = L * r.uniform(.35, .5); ang = r.uniform(0, math.pi)
            col = pals[q][r.integers(0, 4)]; sh = r.uniform(.75, 1.1)
            pts = []
            for t in np.linspace(0, 2 * math.pi, 18):
                px, py = math.cos(t) * L, math.sin(t) * W * (1 - .35 * abs(math.cos(t)))
                pts.append((cx + px * math.cos(ang) - py * math.sin(ang), cy + px * math.sin(ang) + py * math.cos(ang)))
            d.polygon(pts, fill=tuple(int(c * sh) for c in col) + (255,))
            d.line([pts[0], pts[9]], fill=tuple(int(c * sh * .7) for c in col) + (255,), width=1)
            nx, ny = math.cos(ang + 1.2) * .35, math.sin(ang + 1.2) * .35
            dn.polygon(pts, fill=(int((nx * .5 + .5) * 255), int((-ny * .5 + .5) * 255), 230))
    img = img.filter(ImageFilter.SMOOTH)
    img.save(os.path.join(TEX, 'leaves_a.png')); nrm.save(os.path.join(TEX, 'leaves_n.png'))
    print('wrote leaves')

# ---------- VS-02 liveries: a line-for-line port of drawLivery() from the frozen v0.8 car ----------
LIVERIES = [
    dict(name='TRANSPARENT', base='#ecebe6', ink='#16171a', acc='#d71921', rough=.4),
    dict(name='SECTION', base='#7b7e68', ink='#1b1d18', acc='#f0ac2c', rough=.72, hazard=True),
    dict(name='VINYL', base='#8f8c95', ink='#141416', acc='#ff8fb8', rough=.86, xx=True),
    dict(name='OIL', base='#b8894a', ink='#1d2a2c', acc='#3c7a77', rough=.62, paint=True),
    dict(name='INK', base='#17181a', ink='#f1f1ee', acc='#d4ff00', rough=.3),
]
def hexrgb(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))
def livery(L, idx):
    W, H, z0, z1 = 1024, 512, -2.18, 2.24
    X = lambda z: (z - z0) / (z1 - z0) * W
    Y = lambda u: (1 - u) * H
    img = Image.new('RGBA', (W, H), hexrgb(L['base']) + (255,))
    r = np.random.default_rng(100 + idx)
    ov = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    if L.get('paint'):
        cols = ['#c99a58', '#9c7240', '#d8b678', '#4b7d78', '#7a5a34']
        for i in range(1400):
            x, y, a, l = r.random() * W, r.random() * H, (r.random() - .5) * .6, 14 + r.random() * 40
            al = int((.08 + r.random() * .16) * 255); lw = int(2 + r.random() * 6)
            d.line([(x, y), (x + math.cos(a) * l, y + math.sin(a) * l)], fill=hexrgb(cols[i % 5]) + (al,), width=lw)
        img = Image.alpha_composite(img, ov); ov = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    else:
        a = np.array(img).astype(np.float32); amp = 10 if L['rough'] > .6 else 4
        nz = (r.random((H, W)) - .5) * amp
        a[..., :3] = np.clip(a[..., :3] + nz[..., None], 0, 255); img = Image.fromarray(a.astype(np.uint8), 'RGBA')
    def rect(x0, y0, w, h, col, al):
        d.rectangle([x0, y0, x0 + w - 1, y0 + h - 1], fill=hexrgb(col) + (int(al * 255),) if col.startswith('#') else col)
    for z in (-1.92, -0.84, 0.86, 1.88):
        d.rectangle([X(z) - 1, 0, X(z) + 1.2, H], fill=(0, 0, 0, 107)); d.rectangle([X(z) + 1.4, 0, X(z) + 2.4, H], fill=(255, 255, 255, 41))
    for u in (.34, .66): d.rectangle([X(-2.0), Y(u) - 1, X(1.86), Y(u) + .8], fill=(0, 0, 0, 77))
    d.rectangle([0, Y(.1), W, Y(0)], fill=(0, 0, 0, 89)); d.rectangle([0, 0, W, Y(.9)], fill=(0, 0, 0, 89))
    ink = hexrgb(L['ink']); acc = hexrgb(L['acc'])
    d.rectangle([X(-1.9), Y(.5) - 1.5, X(-1.3), Y(.5) + 1.5], fill=ink + (217,)); d.rectangle([X(.95), Y(.5) - 1.5, X(2.05), Y(.5) + 1.5], fill=ink + (217,))
    for i in range(10):
        for dy in (12, -12): cx, cy = X(1.25) + i * 11, Y(.5) + dy; d.ellipse([cx - 2.6, cy - 2.6, cx + 2.6, cy + 2.6], fill=ink + (217,))
    cx, cy = X(1.6), Y(.5); d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=acc + (255,))
    img = Image.alpha_composite(img, ov); ov = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    mich = ImageFont.truetype(os.path.join(FONTS, 'Michroma-Regular.ttf'), 40)
    plex = lambda s: ImageFont.truetype(os.path.join(FONTS, 'IBMPlexMono-SemiBold.ttf'), s)
    def side(sx, sy):
        nonlocal img
        ux = lambda uu: 1 - uu if sy < 0 else uu
        def T(txt, zEnd, uu, font, col, al):
            nonlocal img
            bb = font.getbbox(txt); w, h = bb[2] - bb[0] + 4, int(font.size * 1.6)
            t = Image.new('RGBA', (w, h), (0, 0, 0, 0)); ImageDraw.Draw(t).text((2 - bb[0], h / 2), txt, font=font, fill=hexrgb(col) + (int(al * 255),), anchor='lm')
            if sx < 0: t = t.transpose(Image.FLIP_LEFT_RIGHT)
            if sy < 0: t = t.transpose(Image.FLIP_TOP_BOTTOM)
            img.alpha_composite(t, (int(X(zEnd) - w), int(Y(ux(uu)) - h / 2)))
        T('VS—02', 1.7, .275, mich, L['ink'], .92)
        T('JET-CAR / FREESTYLE / Nº 0827', 1.7, .215, plex(14), L['ink'], .7)
        T('CAUTION · JET BLAST · KEEP 6 M CLEAR' if L.get('hazard') else 'THRUST 22 KN · DRY 1200 KG', -0.55, .275, plex(13), L['ink'], .62)
        dd = ImageDraw.Draw(img, 'RGBA')
        for i in range(7):
            for j in range(4): cx, cy = X(-.5 + i * .045), Y(ux(.2 + j * .02)); dd.ellipse([cx - 2.4, cy - 2.4, cx + 2.4, cy + 2.4], fill=ink + (191,))
        if L.get('hazard'):
            x0, x1, y0 = X(-2.0), X(-1.6), Y(ux(.25))
            stripe = Image.new('RGBA', (int(x1 - x0), 26), acc + (255,)); sd = ImageDraw.Draw(stripe)
            for k in range(-2, 8): sd.polygon([(k * 18, 26), (k * 18 + 9, 26), (k * 18 + 35, 0), (k * 18 + 26, 0)], fill=ink + (255,))
            img.alpha_composite(stripe, (int(x0), int(y0 - 13)))
        if L.get('xx'):
            for zz in (-1.75, -1.45):
                x, y = X(zz), Y(ux(.25))
                dd.line([(x - 11, y - 11), (x + 11, y + 11)], fill=acc + (255,), width=7); dd.line([(x + 11, y - 11), (x - 11, y + 11)], fill=acc + (255,), width=7)
                for px, py in [(x - 11, y - 11), (x + 11, y + 11), (x + 11, y - 11), (x - 11, y + 11)]: dd.ellipse([px - 3.5, py - 3.5, px + 3.5, py + 3.5], fill=acc + (255,))
        dd.rectangle([X(-1.95), Y(ux(.15)) - 2, X(1.5), Y(ux(.15)) + 2], fill=acc + (255,))
    side(-1, 1); side(1, -1)
    img.convert('RGB').save(os.path.join(TEX, f'livery{idx}_a.png'))
    print('wrote livery', L['name'])

# ---------- the canopy board (product diagram under the smoked glass) ----------
def board():
    W, H = 256, 512; img = Image.new('RGB', (W, H), (42, 43, 45)); d = ImageDraw.Draw(img)
    c = (240, 240, 236); cs = (int(240 * .55 + 42 * .45),) * 3
    d.rectangle([18, 18, 238, 494], outline=cs, width=2)
    for rr in (62, 40): d.ellipse([128 - rr, 400 - rr, 128 + rr, 400 + rr], outline=cs, width=2)
    d.rectangle([46, 120, 210, 290], outline=cs, width=2)
    for i in range(6): d.line([(60 + i * 27, 300), (60 + i * 27, 330), (128, 338)], fill=cs, width=2)
    for i in range(9):
        for j in range(4): d.ellipse([60 + i * 17 - 2.2, 50 + j * 14 - 2.2, 60 + i * 17 + 2.2, 50 + j * 14 + 2.2], fill=(int(240 * .6 + 42 * .4),) * 3)
    f = ImageFont.truetype(os.path.join(FONTS, 'IBMPlexMono-SemiBold.ttf'), 16)
    d.text((30, 470), 'VS—02', font=f, fill=(int(240 * .6 + 42 * .4),) * 3, anchor='ls'); d.text((150, 470), 'JET / 22', font=f, fill=(int(240 * .6 + 42 * .4),) * 3, anchor='ls')
    img.save(os.path.join(TEX, 'board_a.png')); print('wrote board')

if __name__ == '__main__':
    concrete(); asphalt(); moss(); rock(); sand(); steel(); bark(); noise_tex(); leaves(); board()
    for i, L in enumerate(LIVERIES): livery(L, i)
