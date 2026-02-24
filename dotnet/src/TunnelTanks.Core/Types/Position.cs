namespace TunnelTanks.Core.Types;

public readonly record struct Position(int X, int Y)
{
    public static Position operator +(Position a, Offset b) => new(a.X + b.X, a.Y + b.Y);
    public static Position operator -(Position a, Offset b) => new(a.X - b.X, a.Y - b.Y);
    public static Offset operator -(Position a, Position b) => new(a.X - b.X, a.Y - b.Y);
    public static int DistanceSquared(Position a, Position b) { var d = a - b; return d.X * d.X + d.Y * d.Y; }
}

public readonly record struct Offset(int X, int Y)
{
    public static Offset operator +(Offset a, Offset b) => new(a.X + b.X, a.Y + b.Y);
    public static Offset operator -(Offset a, Offset b) => new(a.X - b.X, a.Y - b.Y);
    public static Offset operator *(Offset a, int s) => new(a.X * s, a.Y * s);
}

public readonly record struct Size(int X, int Y)
{
    public int Area => X * Y;
    public static Size operator /(Size s, int d) => new(s.X / d, s.Y / d);
    public static Size operator *(Size s, int m) => new(s.X * m, s.Y * m);
    public bool FitsInside(Position pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < X && pos.Y < Y;
}

public readonly record struct PositionF(float X, float Y)
{
    public static PositionF operator +(PositionF a, VectorF b) => new(a.X + b.X, a.Y + b.Y);
    public static PositionF operator -(PositionF a, VectorF b) => new(a.X - b.X, a.Y - b.Y);
    public static VectorF operator -(PositionF a, PositionF b) => new(a.X - b.X, a.Y - b.Y);
    public static explicit operator PositionF(Position p) => new(p.X, p.Y);
    public static explicit operator Position(PositionF p) => new((int)p.X, (int)p.Y);
}

public readonly record struct VectorF(float X, float Y)
{
    public float LengthSquared => X * X + Y * Y;
    public float Length => MathF.Sqrt(LengthSquared);
    public VectorF Normalized => Length > 0 ? new(X / Length, Y / Length) : default;
    public static VectorF operator *(VectorF v, float s) => new(v.X * s, v.Y * s);
    public static VectorF operator +(VectorF a, VectorF b) => new(a.X + b.X, a.Y + b.Y);
}

public readonly record struct DirectionF(float X, float Y)
{
    public static DirectionF FromAngle(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));
    public float ToAngle() => MathF.Atan2(Y, X);
    public VectorF ToVector(float speed) => new(X * speed, Y * speed);
}

public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    public uint ToArgb() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;
    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
}

public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public Position Center => new(X + Width / 2, Y + Height / 2);
    public bool IsInside(Position p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;
}

public readonly record struct BoundingBox(int HalfWidth, int HalfHeight)
{
    public bool IsInside(Position tested, Position origin) =>
        Math.Abs(tested.X - origin.X) < HalfWidth && Math.Abs(tested.Y - origin.Y) < HalfHeight;
    public Size Size => new(HalfWidth * 2, HalfHeight * 2);
}
