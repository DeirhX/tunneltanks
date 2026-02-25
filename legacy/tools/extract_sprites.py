"""
One-time sprite extraction from the reference HUD concept image.
Cuts four full-height contiguous module slices that tile seamlessly.
"""
from PIL import Image
import os

SRC = r"C:\Users\doome\.cursor\projects\e-Projects-New-tunnerer\assets\c__Users_doome_AppData_Roaming_Cursor_User_workspaceStorage_1c52c93a0819b20c26eb7c5a97062de9_images_image-445dcca0-8107-41c8-a4a4-b497ee081f9b.png"
DST = r"E:\Projects\New\tunnerer\dotnet\src\TunnelTanks.Desktop\resources\hud"

# Full-height contiguous module slices from the source HUD strip.
# All share the same Y band (148..515 = 367px tall) for seamless joins.
# (name, left, top, right, bottom)
REGIONS = [
    ("energy_icon",    0, 148, 245, 515),
    ("shield_icon",  245, 148, 390, 515),
    ("panel_frame",  390, 148, 620, 515),
    ("build_panel",  620, 148, 1024, 515),
]

def extract():
    os.makedirs(DST, exist_ok=True)
    src = Image.open(SRC).convert("RGBA")
    print(f"Source: {src.size[0]}x{src.size[1]}")

    for name, l, t, r, b in REGIONS:
        l = max(0, l)
        t = max(0, t)
        r = min(src.size[0], r)
        b = min(src.size[1], b)

        crop = src.crop((l, t, r, b))
        path = os.path.join(DST, f"{name}.png")
        crop.save(path)
        print(f"  {name}.png  ({r-l}x{b-t})")

    print(f"\nDone. {len(REGIONS)} sprites saved to {DST}")

if __name__ == "__main__":
    extract()
