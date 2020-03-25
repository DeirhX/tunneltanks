#pragma once

#include <algorithm>
#include <cassert>
#include <cmath>
#include <memory>

/* Generic types that are used all over the place.
 * Most of them are identical and exist solely to enforce sensible naming and conversions
 * so things don't get inadvertently mixed together.
   Conversions possible only when it is conceptually sensible - enforce clear semantics
*/

/* Generic 2D vector. All things decay to it. */
struct Vector
{
    int x = 0, y = 0;
    Vector() = default;
    constexpr Vector(int x, int y) noexcept : x(x), y(y) {}
};

/* Position relative to our logical Screen*/
struct ScreenPosition : public Vector
{
    constexpr ScreenPosition() = default;
    constexpr ScreenPosition(int x, int y) : Vector(x, y) {}
    constexpr explicit ScreenPosition(Vector vec) : Vector(vec) {}
};

/* Position relative to native OS window. */
struct NativeScreenPosition : public Vector
{
    constexpr NativeScreenPosition() = default;
    constexpr NativeScreenPosition(int x, int y) : Vector(x, y) {}
};

struct Position : public Vector
{
    constexpr Position() = default;
    constexpr Position(int x, int y) : Vector(x, y) {}
    explicit Position(ScreenPosition pos) : Vector(pos.x, pos.y) {}
};

struct Size : public Vector
{
    constexpr Size() = default;
    constexpr Size(int sx, int sy) : Vector(sx, sy) {}
};

struct Speed : public Vector
{
    Speed() = default;
    constexpr Speed(int sx, int sy) : Vector(sx, sy) {}
};

struct Offset : public Vector
{
    Offset() = default;
    constexpr Offset(int dx, int dy) : Vector(dx, dy) {}

    explicit Offset(Position pos) : Vector(pos.x, pos.y) {}
};

/*
 *   Vector +- Vector -> Vector
 *   Position - Position -> Offset
 *   Position + Offset -> Position
 *   Speed * scalar -> Offset
 *   Size * scalar -> Size
 */

constexpr Vector operator+(Vector v, Vector o) noexcept { return {v.x + o.x, v.y + o.y}; }
constexpr Vector operator-(Vector v, Vector o) noexcept { return {v.x - o.x, v.y - o.y}; }
constexpr Offset operator*(Speed s, int t) noexcept { return {s.x * t, s.y * t}; }
constexpr Offset operator*(int t, Speed s) noexcept { return {s.x * t, s.y * t}; }
constexpr Size operator+(Size l, Size r) noexcept { return {l.x + r.x, l.y + r.y}; }
constexpr Size operator-(Size l, Size r) noexcept { return {l.x - r.x, l.y - r.y}; }
constexpr Size operator*(Size s, int t) noexcept { return {s.x * t, s.y * t}; }
constexpr Size operator*(int t, Size s) noexcept { return {s.x * t, s.y * t}; }
constexpr Size operator/(Size s, int t) noexcept { return {s.x / t, s.y / t}; }
constexpr Offset operator+(Offset o, Size s) noexcept { return {o.x + s.x, o.y + s.y}; }
constexpr Offset operator-(Offset o, Size s) noexcept { return {o.x - s.x, o.y - s.y}; }
constexpr Offset operator-(Position p, Position o) noexcept { return {p.x - o.x, p.y - o.y}; }
constexpr Position operator+(Position v, Offset o) noexcept { return {v.x + o.x, v.y + o.y}; }
constexpr Position operator+(Position v, Size o) noexcept { return {v.x + o.x, v.y + o.y}; }
constexpr bool operator==(Position l, Position r) noexcept { return l.x == r.x && l.y == r.y; }
constexpr ScreenPosition & operator+=(ScreenPosition & p, Offset o) noexcept
{
    p.x += o.x;
    p.y += o.y;
    return p;
}

/* Integer direction. Take your numerical keyboard and subtract 1 */
struct Direction
{
    int dir;
    [[nodiscard]] int Get() const { return this->dir; }
    int & Get() { return this->dir; }
    void Set(int new_dir) { this->dir = new_dir; }
    [[nodiscard]] Speed ToSpeed() const
    {
        return Speed{static_cast<int>(this->dir) % 3 - 1, static_cast<int>(this->dir) / 3 - 1};
    }
    static Direction FromSpeed(Speed speed) { return Direction{speed.x + 1 + 3 * (speed.y + 1)}; }
    operator int() const { return dir; }
};

/*
 *  Float math. Needed for shooting.
 */

struct VectorF
{
    float x = 0, y = 0;
    VectorF() = default;
    constexpr VectorF(float x, float y) : x(x), y(y) {}

    [[nodiscard]] float GetSize() const { return std::sqrt(x * x + y * y); }
    [[nodiscard]] bool IsNormalized() const { return GetSize() == 1.0f; }
    [[nodiscard]] VectorF Normalize() const
    {
        auto size = GetSize();
        assert(size);
        if (size)
            return {x / size, y / size};
        else
            return {};
    }
};
struct PositionF : public VectorF
{
    PositionF() = default;
    constexpr PositionF(float sx, float sy) : VectorF(sx, sy) {}
    explicit PositionF(Position pos) /* Should become center of the pixel */
        : PositionF{static_cast<float>(pos.x) + 0.5f, static_cast<float>(pos.y) + 0.5f}
    {
    }
    static PositionF FromIntPosition(Position pos) { return PositionF{pos}; }
    [[nodiscard]] Position ToIntPosition() const
    {
        return Position{static_cast<int>(this->x), static_cast<int>(this->y)};
    }
};
struct SpeedF : public VectorF
{
    SpeedF() = default;
    SpeedF(VectorF vector) : VectorF(vector) {}
    constexpr SpeedF(float sx, float sy) : VectorF(sx, sy) {}
};

struct OffsetF : public VectorF
{
    OffsetF() = default;
    OffsetF(VectorF vector) : VectorF(vector) {}
    constexpr OffsetF(float sx, float sy) : VectorF(sx, sy) {}
    explicit OffsetF(Offset int_offset) : VectorF(float(int_offset.x), float(int_offset.y)) {}
    explicit operator Offset() const { return Offset{int(this->x), int(this->y)}; }
};
/* TODO: we need actually just radians. But so often will we use the components it won't hurt to store them instead */
struct DirectionF : VectorF
{
  public:
    DirectionF() = default;
    DirectionF(VectorF vector) : VectorF(vector) {}
    DirectionF(float x, float y) : VectorF(x, y) { assert((x == 0 && y == 0) || this->IsNormalized()); }
    DirectionF(Direction int_direction)
    {
        auto speed = int_direction.ToSpeed();
        this->x = float(speed.x);
        this->y = float(speed.y);
        //assert(this->IsNormalized());
    };
    [[nodiscard]] int ToIntDirection() const { return Direction::FromSpeed({int(x), int(y)}); }
    [[nodiscard]] Speed ToSpeed() const { return Speed{int(x), int(y)}; }
};

constexpr VectorF operator+(VectorF v, VectorF o) noexcept { return {v.x + o.x, v.y + o.y}; }
constexpr VectorF operator-(VectorF v, VectorF o) noexcept { return {v.x - o.x, v.y - o.y}; }
constexpr OffsetF operator*(SpeedF s, float t) noexcept { return {s.x * t, s.y * t}; }
constexpr OffsetF operator*(float t, SpeedF s) noexcept { return {s.x * t, s.y * t}; }
constexpr OffsetF operator*(OffsetF o, float m) noexcept { return {o.x * m, o.y * m}; }
constexpr OffsetF operator*(float m, OffsetF o) noexcept { return {o.x * m, o.y * m}; }
constexpr OffsetF operator/(OffsetF o, float d) noexcept { return {o.x / d, o.y / d}; }
constexpr OffsetF operator-(PositionF p, PositionF o) noexcept { return {p.x - o.x, p.y - o.y}; }
constexpr bool operator==(OffsetF l, OffsetF r) { return l.x == r.x && l.y == r.y; }
constexpr bool operator!=(OffsetF l, OffsetF r) { return l.x != r.x || l.y != r.y; }
constexpr PositionF operator+(PositionF v, OffsetF o) noexcept { return {v.x + o.x, v.y + o.y}; }
constexpr PositionF & operator+=(PositionF v, OffsetF o) noexcept
{
    v.x += o.x;
    v.y += o.y;
    return v;
}
constexpr bool operator==(PositionF l, PositionF r) { return l.x == r.x && l.y == r.y; }
constexpr bool operator!=(PositionF l, PositionF r) { return l.x != r.x || l.y != r.y; }
constexpr OffsetF operator*(DirectionF d, float m) noexcept { return {d.x * m, d.y * m}; }
constexpr OffsetF operator*(float m, DirectionF d) noexcept { return {m * d.x, m * d.y}; }

/* Rectangle inside game world */
struct Rect
{
    Position pos;
    Size size;
    constexpr Rect() = default;
    constexpr Rect(Position pos, Size size) : pos(pos), size(size) {}
    //Rect(ScreenPosition pos, Size size) : pos(pos), size(size) { }
    constexpr Rect(int pos_x, int pos_y, int size_x, int size_y) : pos{pos_x, pos_y}, size{size_x, size_y} {}

    constexpr int Left() const { return pos.x; }
    constexpr int Top() const { return pos.y; }
    constexpr int Right() const { return pos.x + size.x; }
    constexpr int Bottom() const { return pos.y + size.y; }
    constexpr bool IsInside(Vector vec) const
    {
        return vec.x >= this->Left() && vec.x <= this->Right() && vec.y >= this->Top() && vec.y <= this->Bottom();
    }
    [[nodiscard]] Vector MakeInside(Vector vec) const
    {
        return {std::clamp(vec.x, this->Left(), this->Right()), std::clamp(vec.y, this->Top(), this->Bottom())};
    }
    [[nodiscard]] Position Center() const { return {pos.x + size.x / 2, pos.y + size.y / 2}; }
};

struct ScreenRect : Rect
{
    constexpr ScreenRect() = default;
    constexpr ScreenRect(ScreenPosition pos, Size size) : Rect{pos.x, pos.y, size.x, size.y} {}
    constexpr ScreenRect(int pos_x, int pos_y, int size_x, int size_y) : Rect{pos_x, pos_y, size_x, size_y} {}
};

/* Rectangle in native units of hosting window/surface */
struct NativeRect : Rect
{
    NativeRect() = default;
    NativeRect(NativeScreenPosition pos, Size size) : Rect{pos.x, pos.y, size.x, size.y} {}
    NativeRect(int pos_x, int pos_y, int size_x, int size_y) : Rect{pos_x, pos_y, size_x, size_y} {}
};

struct Color
{
    unsigned char r{}, g{}, b{};

  public:
    Color() = default;
    Color(unsigned char r, unsigned char g, unsigned char b) : r(r), g(g), b(b) {}
    Color Mask(Color other) const
    {
        return Color((this->r * other.r) / 255, (this->g * other.g) / 255, (this->b * other.b) / 255);
    }
};

struct Color32
{
    unsigned char r{}, g{}, b{}, a{};
    Color32() = default;
    Color32(Color color) : Color32(color.r, color.g, color.b, 255) {}
    Color32(unsigned char r, unsigned char g, unsigned char b, unsigned char a) : r(r), g(g), b(b), a(a) {}
    Color BlendWith(Color other) const
    {
        if (a == 255)
            return Color(r, g, b);
        return Color((this->r * this->a) / 255 + other.r * (255 - this->a),
                     (this->g * this->a) / 255 + other.g * (255 - this->a),
                     (this->b * this->a) / 255 + other.b * (255 - this->a));
    }
    explicit operator Color() const { return Color(r, g, b); }
};

using TankColor = char;

enum class Orientation
{
    Horizontal,
    Vertical,
};

template <typename Type>
class holder_with_deleter : public std::unique_ptr<Type, void (*)(Type *)>
{
    using base = std::unique_ptr<Type, void (*)(Type *)>;

  public:
    holder_with_deleter() : base{nullptr, [](Type *) {}} {};
    holder_with_deleter(Type * value, void (*deleter)(Type *)) : base{value, deleter} {}
};

template <typename TEnum>
bool HasFlag(TEnum value, TEnum flag)
{
    return (static_cast<std::underlying_type_t<TEnum>>(value) & static_cast<std::underlying_type_t<TEnum>>(value)) != 0;
}