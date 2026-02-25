using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

public class TypeTests
{
    [Fact]
    public void Position_Plus_Offset()
    {
        var p = new Position(3, 5);
        var o = new Offset(2, -1);
        Assert.Equal(new Position(5, 4), p + o);
    }

    [Fact]
    public void Position_Minus_Offset()
    {
        var p = new Position(10, 8);
        var o = new Offset(3, 2);
        Assert.Equal(new Position(7, 6), p - o);
    }

    [Fact]
    public void Position_Minus_Position_GivesOffset()
    {
        var a = new Position(10, 8);
        var b = new Position(3, 2);
        Assert.Equal(new Offset(7, 6), a - b);
    }

    [Fact]
    public void Position_DistanceSquared()
    {
        var a = new Position(0, 0);
        var b = new Position(3, 4);
        Assert.Equal(25, Position.DistanceSquared(a, b));
    }

    [Fact]
    public void BoundingBox_IsInside()
    {
        var bb = new BoundingBox(5, 5);
        var origin = new Position(10, 10);

        Assert.True(bb.IsInside(new Position(10, 10), origin));
        Assert.True(bb.IsInside(new Position(11, 11), origin));
        Assert.True(bb.IsInside(new Position(6, 6), origin));
        Assert.False(bb.IsInside(new Position(5, 5), origin)); // exactly at half-width
        Assert.False(bb.IsInside(new Position(15, 15), origin));
    }

    [Fact]
    public void Rect_IsInside()
    {
        var r = new Rect(5, 5, 10, 10);

        Assert.True(r.IsInside(new Position(5, 5)));
        Assert.True(r.IsInside(new Position(14, 14)));
        Assert.False(r.IsInside(new Position(15, 15))); // right/bottom is exclusive
        Assert.False(r.IsInside(new Position(4, 5)));
    }

    [Fact]
    public void Rect_Properties()
    {
        var r = new Rect(2, 3, 10, 20);
        Assert.Equal(2, r.Left);
        Assert.Equal(3, r.Top);
        Assert.Equal(12, r.Right);
        Assert.Equal(23, r.Bottom);
        Assert.Equal(new Position(7, 13), r.Center);
    }

    [Fact]
    public void Size_Area()
    {
        Assert.Equal(600, new Size(30, 20).Area);
    }

    [Fact]
    public void Size_FitsInside()
    {
        var s = new Size(10, 8);
        Assert.True(s.FitsInside(new Position(0, 0)));
        Assert.True(s.FitsInside(new Position(9, 7)));
        Assert.False(s.FitsInside(new Position(10, 7)));
        Assert.False(s.FitsInside(new Position(-1, 0)));
    }

    [Fact]
    public void Color_ToArgb()
    {
        var c = new Color(255, 128, 64);
        Assert.Equal(0xFF_FF_80_40u, c.ToArgb());
    }

    [Fact]
    public void Color_TransparentAlpha()
    {
        Assert.Equal(0u, Color.Transparent.ToArgb());
    }

    [Fact]
    public void Offset_Arithmetic()
    {
        var a = new Offset(3, 4);
        var b = new Offset(1, 2);
        Assert.Equal(new Offset(4, 6), a + b);
        Assert.Equal(new Offset(2, 2), a - b);
        Assert.Equal(new Offset(6, 8), a * 2);
    }

    [Fact]
    public void VectorF_Length()
    {
        var v = new VectorF(3f, 4f);
        Assert.Equal(25f, v.LengthSquared);
        Assert.Equal(5f, v.Length, 0.001f);
    }

    [Fact]
    public void VectorF_Normalized()
    {
        var v = new VectorF(0f, 5f);
        var n = v.Normalized;
        Assert.Equal(0f, n.X, 0.001f);
        Assert.Equal(1f, n.Y, 0.001f);
    }

    [Fact]
    public void DirectionF_FromAngle_RoundTrip()
    {
        float angle = 1.23f;
        var d = DirectionF.FromAngle(angle);
        Assert.Equal(angle, d.ToAngle(), 0.001f);
    }

    [Fact]
    public void PositionF_Conversion()
    {
        var p = new Position(7, 11);
        var pf = (PositionF)p;
        Assert.Equal(7f, pf.X);
        Assert.Equal(11f, pf.Y);

        var back = (Position)pf;
        Assert.Equal(p, back);
    }
}
