namespace Tunnerer.Tests;

using System.Globalization;
using Tunnerer.Core;
using Tunnerer.Core.Terrain;

internal sealed class TestVisualTrace : IDisposable
{
    private static readonly bool EnabledByEnv =
        string.Equals(Environment.GetEnvironmentVariable("TUNNERER_TEST_VISUAL"), "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("TUNNERER_TEST_VISUAL"), "true", StringComparison.OrdinalIgnoreCase);

    private static readonly int CaptureStride = ParsePositiveInt(
        Environment.GetEnvironmentVariable("TUNNERER_TEST_VISUAL_EVERY"), fallback: 1);

    private readonly string _outputDir;
    private readonly bool _enabled;
    private int _frame;

    private TestVisualTrace(string outputDir, bool enabled)
    {
        _outputDir = outputDir;
        _enabled = enabled;
    }

    public static TestVisualTrace Start(string testName)
    {
        if (!EnabledByEnv)
            return new TestVisualTrace(outputDir: string.Empty, enabled: false);

        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string safeName = Sanitize(testName);
        string outputDir = Path.Combine(
            AppContext.BaseDirectory,
            "artifacts",
            "test-visual",
            $"{stamp}_{safeName}");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "README.txt"),
            "Frames are binary PPM (P6). Open with image viewers or convert with ffmpeg/imagemagick.");
        Console.WriteLine($"[test-visual] writing frames to: {outputDir}");
        return new TestVisualTrace(outputDir, enabled: true);
    }

    public void Capture(World world, string label)
    {
        if (!_enabled)
            return;
        if ((_frame % CaptureStride) != 0)
        {
            _frame++;
            return;
        }

        var terrain = world.Terrain;
        int width = terrain.Width;
        int height = terrain.Height;
        byte[] rgb = new byte[width * height * 3];

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int idx = row + x;
                int dst = idx * 3;
                var pix = terrain.GetPixelRaw(idx);
                var baseColor = Pixel.GetColor(pix);
                float heat = terrain.GetHeatTemperature(idx);

                // Blend terrain albedo with heat glow to make transfer patterns obvious.
                float h = Math.Clamp(heat / 255f, 0f, 1f);
                int r = ClampByte(baseColor.R + (int)(230f * h));
                int g = ClampByte((int)(baseColor.G * (1f - 0.55f * h)));
                int b = ClampByte((int)(baseColor.B * (1f - 0.75f * h)));

                rgb[dst + 0] = (byte)r;
                rgb[dst + 1] = (byte)g;
                rgb[dst + 2] = (byte)b;
            }
        }

        foreach (var tank in world.TankList.Tanks)
        {
            int tx = tank.Position.X;
            int ty = tank.Position.Y;
            if (tx < 0 || ty < 0 || tx >= width || ty >= height)
                continue;
            int dst = (tx + ty * width) * 3;
            rgb[dst + 0] = 64;
            rgb[dst + 1] = 180;
            rgb[dst + 2] = 255;
        }

        string safeLabel = Sanitize(label);
        string file = Path.Combine(_outputDir, $"{_frame:D6}_{safeLabel}.ppm");
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var bw = new BinaryWriter(fs);
        bw.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
        bw.Write(rgb);
        _frame++;
    }

    public void Dispose()
    {
    }

    private static int ParsePositiveInt(string? value, int fallback)
    {
        if (int.TryParse(value, out int parsed) && parsed > 0)
            return parsed;
        return fallback;
    }

    private static int ClampByte(int v) => v < 0 ? 0 : (v > 255 ? 255 : v);

    private static string Sanitize(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        Span<char> buffer = stackalloc char[input.Length];
        int j = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            bool isInvalid = false;
            for (int k = 0; k < invalid.Length; k++)
            {
                if (c == invalid[k])
                {
                    isInvalid = true;
                    break;
                }
            }

            buffer[j++] = isInvalid || char.IsWhiteSpace(c) ? '_' : c;
        }
        return new string(buffer[..j]);
    }
}
