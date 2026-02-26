namespace Tunnerer.Desktop.Rendering;

public sealed class HiResEntityRenderer
{
    public void Render(
        uint[] targetPixels,
        int targetWidth,
        int targetHeight,
        uint[] terrainPixels,
        uint[] compositePixels,
        int worldWidth,
        int worldHeight,
        int camPixelX,
        int camPixelY,
        int pixelScale)
    {
        int cellMinX = Math.Max(0, camPixelX / pixelScale);
        int cellMinY = Math.Max(0, camPixelY / pixelScale);
        int cellMaxX = Math.Min(worldWidth - 1, (camPixelX + targetWidth - 1) / pixelScale);
        int cellMaxY = Math.Min(worldHeight - 1, (camPixelY + targetHeight - 1) / pixelScale);

        for (int wy = cellMinY; wy <= cellMaxY; wy++)
        {
            int worldRow = wy * worldWidth;
            for (int wx = cellMinX; wx <= cellMaxX; wx++)
            {
                int idx = worldRow + wx;
                uint terrainColor = terrainPixels[idx];
                uint objectColor = compositePixels[idx];
                if (objectColor == terrainColor)
                    continue;

                int baseScreenX = wx * pixelScale - camPixelX;
                int baseScreenY = wy * pixelScale - camPixelY;

                int startPx = Math.Max(0, -baseScreenX);
                int endPx = Math.Min(pixelScale, targetWidth - baseScreenX);
                int startPy = Math.Max(0, -baseScreenY);
                int endPy = Math.Min(pixelScale, targetHeight - baseScreenY);

                for (int py = startPy; py < endPy; py++)
                {
                    int row = (baseScreenY + py) * targetWidth + baseScreenX;
                    for (int px = startPx; px < endPx; px++)
                        targetPixels[row + px] = (targetPixels[row + px] & 0xFF000000u) | (objectColor & 0x00FFFFFFu);
                }
            }
        }
    }
}
