namespace Tunnerer.Core.Types;

public static class RectMath
{
    public static Rect FromMinMaxInclusive(int minX, int minY, int maxX, int maxY) =>
        new(minX, minY, maxX - minX + 1, maxY - minY + 1);

    public static void GetMinMaxInclusive(in Rect rect, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = rect.Left;
        minY = rect.Top;
        maxX = rect.Right - 1;
        maxY = rect.Bottom - 1;
    }

    public static Rect Union(in Rect a, in Rect b)
    {
        int minX = Math.Min(a.Left, b.Left);
        int minY = Math.Min(a.Top, b.Top);
        int maxX = Math.Max(a.Right, b.Right);
        int maxY = Math.Max(a.Bottom, b.Bottom);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public static Rect? Union(Rect? a, Rect? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return Union(a.Value, b.Value);
    }
}
