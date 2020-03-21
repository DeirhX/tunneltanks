#pragma once

#include <cassert>
#include <cmath>
#include <memory>

/* Generic types that are used all over the place. 
   Conversions possible only when it is conceptually sensible - enforce clear semantics
*/

/* Generic 2D vector. All things decay to it. */
struct Vector
{
    int x = 0, y = 0;
    Vector() = default;
    constexpr Vector(int x, int y) : x(x), y(y) {}
};

/* Position relative to our logical Screen*/
struct ScreenPosition : public Vector
{
    ScreenPosition() = default;
    ScreenPosition(int x, int y) : Vector(x, y) {}
};

/* Position relative to native OS window. */
struct NativeScreenPosition : public Vector
{
    NativeScreenPosition() = default;
    NativeScreenPosition(int x, int y) : Vector(x, y) {}
};

struct Position : public Vector
{
    Position() = default;
    Position(int x, int y) : Vector(x, y) {}
    explicit Position(ScreenPosition pos) : Vector(pos.x, pos.y) {}
};

struct Size : public Vector
{
    Size() = default;
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

inline Vector operator+(Vector v, Vector o) { return {v.x + o.x, v.y + o.y}; }
inline Vector operator-(Vector v, Vector o) { return {v.x - o.x, v.y - o.y}; }
inline Offset operator*(Speed s, int t) { return {s.x * t, s.y * t}; }
inline Offset operator*(int t, Speed s) { return {s.x * t, s.y * t}; }
inline Size operator*(Size s, int t) { return {s.x * t, s.y * t}; }
inline Size operator*(int t, Size s) { return {s.x * t, s.y * t}; }
inline Size operator/(Size s, int t) { return {s.x / t, s.y / t}; }
inline Offset operator-(Position p, Position o) { return {p.x - o.x, p.y - o.y}; }
inline Position operator+(Position v, Offset o) { return {v.x + o.x, v.y + o.y}; }
inline Position operator+(Position v, Size o) { return {v.x + o.x, v.y + o.y}; }
inline bool operator==(Position l, Position r) { return l.x == r.x && l.y == r.y; }

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
        : PositionF{static_cast<float>(pos.x) + 0.5f, static_cast<float>(pos.y) + 0.5f} {} 
    static PositionF FromIntPosition(Position pos) { return PositionF{pos}; }
    [[nodiscard]] Position ToIntPosition() const { return Position{static_cast<int>(this->x), static_cast<int>(this->y)}; }
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
};
inline VectorF operator+(VectorF v, VectorF o) { return {v.x + o.x, v.y + o.y}; }
inline VectorF operator-(VectorF v, VectorF o) { return {v.x - o.x, v.y - o.y}; }
inline OffsetF operator*(SpeedF s, float t) { return {s.x * t, s.y * t}; }
inline OffsetF operator*(float t, SpeedF s) { return {s.x * t, s.y * t}; }
inline OffsetF operator*(OffsetF o, float m) { return {o.x * m, o.y * m}; }
inline OffsetF operator*(float m, OffsetF o) { return {o.x * m, o.y * m}; }
inline OffsetF operator/(OffsetF o, float d) { return {o.x / d, o.y / d}; }
inline OffsetF operator-(PositionF p, PositionF o) { return {p.x - o.x, p.y - o.y}; }
inline PositionF operator+(PositionF v, OffsetF o) { return {v.x + o.x, v.y + o.y}; }
inline PositionF & operator+=(PositionF v, OffsetF o) { v.x += o.x; v.y += o.y; return v; }
inline bool operator==(PositionF l, PositionF r) { return l.x == r.x && l.y == r.y; }

/* Oh no, a float! Can't we do without? */
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

/* Rectangle inside game world */
struct Rect
{
    Position pos;
    Size size;
    Rect() = default;
    Rect(Position pos, Size size) : pos(pos), size(size) {}
    //Rect(ScreenPosition pos, Size size) : pos(pos), size(size) { }
    Rect(int pos_x, int pos_y, int size_x, int size_y) : pos{pos_x, pos_y}, size{size_x, size_y} {}

    int Left() const { return pos.x; }
    int Top() const { return pos.y; }
    int Right() const { return pos.x + size.x; }
    int Bottom() const { return pos.y + size.y; }
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