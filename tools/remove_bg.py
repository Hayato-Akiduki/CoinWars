"""キャラ画像の背景（描き込まれた市松模様や白背景）を透明にして img/ に保存する。

使い方:
    python tools/remove_bg.py 元画像 キャラのキー
    例) python tools/remove_bg.py C:/Downloads/kenshi.webp swordman

画像の外周からつながっている「白〜薄い灰色」の部分だけを透明にする。
キャラの白い服などは輪郭線で囲まれているので消えない。
すでに本当に透明な画像なら、縮小だけして保存する。
"""
import sys
from collections import deque
from pathlib import Path

from PIL import Image, ImageFilter

SIZE = 512  # 保存サイズ（正方形）


def is_bg(c):
    # 白(254)と灰(203)の市松模様、または白背景を背景とみなす
    return min(c) > 178 and max(c) - min(c) < 14


def remove_bg(img):
    img = img.convert('RGB')
    w, h = img.size
    px = img.load()
    seen = bytearray(w * h)
    q = deque([(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)])
    while q:
        x, y = q.popleft()
        i = y * w + x
        if seen[i] or not is_bg(px[x, y]):
            continue
        seen[i] = 1
        if x > 0: q.append((x - 1, y))
        if x < w - 1: q.append((x + 1, y))
        if y > 0: q.append((x, y - 1))
        if y < h - 1: q.append((x, y + 1))
    alpha = Image.frombytes('L', (w, h), bytes(0 if s else 255 for s in seen))
    # 縁の白いにじみを1px削ってから、ほんの少しぼかして輪郭をなめらかに
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    out = img.convert('RGBA')
    out.putalpha(alpha)
    return out


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)
    src, key = Path(sys.argv[1]), sys.argv[2]
    img = Image.open(src)
    img = img.resize((SIZE, SIZE), Image.LANCZOS) if img.size != (SIZE, SIZE) else img
    if 'A' in img.getbands() and img.getchannel('A').getextrema()[0] < 255:
        out = img.convert('RGBA')  # もともと透明
    else:
        out = remove_bg(img)
    dst = Path(__file__).resolve().parent.parent / 'img' / f'{key}.png'
    dst.parent.mkdir(exist_ok=True)
    out.save(dst, optimize=True)
    print(f'保存しました: {dst} ({dst.stat().st_size // 1024}KB)')


if __name__ == '__main__':
    main()
