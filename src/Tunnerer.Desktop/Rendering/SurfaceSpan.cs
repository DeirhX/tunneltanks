namespace Tunnerer.Desktop.Rendering;

using System.Runtime.CompilerServices;

public readonly ref struct SurfaceSpan
{
    private readonly Span<uint> _pixels;

    public int Width { get; }
    public int Height { get; }

    public SurfaceSpan(uint[] pixels, int width, int height)
    {
        _pixels = pixels;
        Width = width;
        Height = height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RowStart(int y) => y * Width;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref uint AtIndex(int index) => ref _pixels[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref uint At(int x, int y) => ref _pixels[x + y * Width];
}
