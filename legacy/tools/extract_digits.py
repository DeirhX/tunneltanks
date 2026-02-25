"""
Extract digit sprites 0-9 from the green-on-purple digit sheet.
Each digit is isolated and alpha-keyed independently to prevent
glow bleed between adjacent cells. Pre-scaled to target height.
"""
from PIL import Image
import numpy as np
import os

SRC = r"C:\Users\doome\.cursor\projects\e-Projects-New-tunnerer\assets\c__Users_doome_AppData_Roaming_Cursor_User_workspaceStorage_1c52c93a0819b20c26eb7c5a97062de9_images_image-e6e7c697-ff5e-4827-9e1d-13e2e8c1de20.png"
DST = r"E:\Projects\New\tunnerer\dotnet\src\TunnelTanks.Desktop\resources\hud\digits.png"

TARGET_W = 17
TARGET_H = 20
DEBUG_DIR = r"E:\Projects\New\tunnerer\dotnet\src\TunnelTanks.Desktop\resources\hud\digits_debug"

img = Image.open(SRC).convert("RGB")
arr = np.array(img).astype(np.float32)
h, w = arr.shape[:2]
print(f"Source: {w}x{h}")

# Find digit band via green-dominant bright pixels
g_dom = arr[:, :, 1] - np.maximum(arr[:, :, 0], arr[:, :, 2])
is_digit_core = (g_dom > 30) & (arr[:, :, 1] > 100)
rows = np.sum(is_digit_core, axis=1)
valid = rows > 3
y_min = max(0, int(np.argmax(valid)) - 15)
y_max = min(h - 1, int(len(valid) - np.argmax(valid[::-1]) - 1) + 15)
band_h = y_max - y_min + 1
print(f"Content band: y={y_min}..{y_max} ({band_h}px)")

# Find horizontal span of the whole 0..9 sequence, then split that span.
cols = np.sum(is_digit_core[y_min:y_max + 1, :], axis=0)
x_valid = cols > 3
x_min = max(0, int(np.argmax(x_valid)) - 8)
x_max = min(w - 1, int(len(x_valid) - np.argmax(x_valid[::-1]) - 1) + 8)
digits_w = x_max - x_min + 1
src_cell_wf = digits_w / 10.0
print(f"Digit span: x={x_min}..{x_max} ({digits_w}px), cell~{src_cell_wf:.2f}px")

print(f"Target cell: {TARGET_W}x{TARGET_H}")

bg = np.array([60.0, 31.0, 95.0])

# Process each digit independently to avoid glow bleed
strip = Image.new("RGBA", (TARGET_W * 10, TARGET_H), (0, 0, 0, 0))

for i in range(10):
    x0 = int(round(x_min + i * src_cell_wf))
    x1 = int(round(x_min + (i + 1) * src_cell_wf))
    x0 = max(0, min(w - 1, x0))
    x1 = max(x0 + 1, min(w, x1))
    src_cell_w = x1 - x0
    cell = arr[y_min:y_max + 1, x0:x1, :]

    dist = np.sqrt(np.sum((cell - bg) ** 2, axis=2))

    # Aggressive alpha to prevent residual glow fragments in empty areas.
    alpha = np.clip((dist - 78) / 36 * 255, 0, 255).astype(np.uint8)

    rgba = np.zeros((band_h, src_cell_w, 4), dtype=np.uint8)
    rgba[:, :, :3] = cell.astype(np.uint8)
    rgba[:, :, 3] = alpha

    cell_img = Image.fromarray(rgba, "RGBA")

    # Crop to actual glyph bounds first, then fit inside target cell with 1px side padding.
    a = rgba[:, :, 3]
    ys, xs = np.where(a > 20)
    if len(xs) == 0:
        fitted = Image.new("RGBA", (TARGET_W, TARGET_H), (0, 0, 0, 0))
    else:
        gx0 = max(0, int(xs.min()) - 1)
        gx1 = min(src_cell_w, int(xs.max()) + 2)
        gy0 = max(0, int(ys.min()) - 1)
        gy1 = min(band_h, int(ys.max()) + 2)
        glyph = cell_img.crop((gx0, gy0, gx1, gy1))

        gw, gh = glyph.size
        draw_w = TARGET_W - 2
        draw_h = TARGET_H
        scale = min(draw_w / gw, draw_h / gh)
        nw = max(1, int(round(gw * scale)))
        nh = max(1, int(round(gh * scale)))
        glyph_scaled = glyph.resize((nw, nh), Image.LANCZOS)

        fitted = Image.new("RGBA", (TARGET_W, TARGET_H), (0, 0, 0, 0))
        px = 1 + (draw_w - nw) // 2
        py = (TARGET_H - nh) // 2
        fitted.paste(glyph_scaled, (px, py))

    strip.paste(fitted, (i * TARGET_W, 0))
    # Save debug cells for visual verification.
    os.makedirs(DEBUG_DIR, exist_ok=True)
    fitted.save(f"{DEBUG_DIR}\\digit_{i}.png")

strip.save(DST)
total_w = TARGET_W * 10
print(f"Saved {total_w}x{TARGET_H} strip ({TARGET_W}x{TARGET_H} per cell)")

if __name__ == "__main__":
    pass
