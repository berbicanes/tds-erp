#!/usr/bin/env python3
"""Generates the PWA icons in web/public (standard library only, no packages).
Blue rounded square with "TDS" in a blocky 5x7 font. Run: python3 scripts/make-icons.py"""
import struct, zlib, os

BLUE = (29, 78, 216, 255)      # Tailwind blue-700, the app's primary colour
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)
FONT = {
    "T": ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    "D": ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    "S": ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
}

def icon(size, rounded):
    px = [[BLUE] * size for _ in range(size)]
    if rounded:  # transparent corners
        r = size * 0.18
        for y in range(size):
            for x in range(size):
                cx = min(max(x + 0.5, r), size - r); cy = min(max(y + 0.5, r), size - r)
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 > r * r:
                    px[y][x] = CLEAR
    text = "TDS"; cols = len(text) * 5 + (len(text) - 1)  # 1 column gap
    cell = int(size * 0.62 / cols)  # text fits the maskable safe zone
    x0 = (size - cols * cell) // 2; y0 = (size - 7 * cell) // 2
    for i, ch in enumerate(text):
        for row, bits in enumerate(FONT[ch]):
            for col, bit in enumerate(bits):
                if bit == "1":
                    for yy in range(cell):
                        for xx in range(cell):
                            px[y0 + row * cell + yy][x0 + (i * 6 + col) * cell + xx] = WHITE
    raw = b"".join(b"\x00" + b"".join(bytes(p) for p in line) for line in px)
    chunk = lambda t, d: struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")

out = os.path.join(os.path.dirname(__file__), "..", "web", "public")
for name, size, rounded in [("icon-192.png", 192, True), ("icon-512.png", 512, True), ("icon-maskable-512.png", 512, False), ("apple-touch-icon.png", 180, False)]:
    with open(os.path.join(out, name), "wb") as f:
        f.write(icon(size, rounded))
    print(name)
