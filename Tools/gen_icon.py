"""App icon: paper-white field, dot-matrix VS in ink, a volt rocket dot and the red record dot. 1024 px, no alpha (App Store rule)."""
import os
from PIL import Image, ImageDraw
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
G = {'V': [17, 17, 17, 17, 17, 10, 4], 'S': [15, 16, 16, 14, 1, 1, 30]}
N = 1024; S = 4  # supersample
im = Image.new('RGB', (N * S, N * S), (238, 236, 230)); d = ImageDraw.Draw(im)
pitch = 66 * S; r = 25 * S
cols = 11; x0 = (N * S - (cols - 1) * pitch) / 2; y0 = (N * S - 6 * pitch) / 2 - 20 * S
for ci, ch in enumerate('VS'):
    for row in range(7):
        for c in range(5):
            x = x0 + (ci * 6 + c) * pitch; y = y0 + row * pitch
            on = (G[ch][row] >> (4 - c)) & 1
            if on: d.ellipse([x - r, y - r, x + r, y + r], fill=(18, 19, 20))
            else: d.ellipse([x - r * .28, y - r * .28, x + r * .28, y + r * .28], fill=(206, 204, 198))
# volt bar and red dot
d.rounded_rectangle([x0 - r, y0 + 8.2 * pitch, x0 + 10 * pitch + r, y0 + 8.2 * pitch + 22 * S], radius=11 * S, fill=(212, 255, 0))
d.ellipse([N * S - 150 * S, 110 * S, N * S - 110 * S, 150 * S], fill=(255, 26, 26))
im = im.resize((N, N), Image.LANCZOS)
im.save(os.path.join(ROOT, 'Assets', 'Icons', 'icon_1024.png'))
print('icon ok')
