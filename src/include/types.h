#pragma once

#include <algorithm>
#include <cassert>
#include <cmath>
#include <chrono>
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

/* Position relative to our logical Screen (our logical render surface pixels) */
struct ScreenPosition : public Vector
{
    constexpr ScreenPosition() = default;
    constexpr ScreenPosition(int x, int y) : Vector(x, y) {}
    constexpr explicit ScreenPosition(Vector vec) : Vector(vec) {}
};

/* Position relative to native OS window in its own units (physical device pixels) */
struct NativeScreenPosition : public Vector
{
    constexpr NativeScreenPosition() = default;
    constexpr NativeScreenPosition(int x, int y) : Vector(x, y) {}
};

/* Position in the level */
struct Position : public Vector
{
    constexpr Position() = default;
    constexpr Position(int x, int y) : Vector(x, y) {}
    explicit Position(ScreenPosition pos) : Vector(pos.x, pos.y) {}
};

/* Size of objects in the world */
struct Size : public Vector
{
    constexpr Size() = default;
    constexpr Size(int sx, int sy) : Vector(sx, sy) {}
    bool FitsInside(int sx, int sy) { return sx >= 0 && sy >= 0 && sx < this->x && sy < this->y; }
};

/* Size in units of our screen render surface */
struct ScreenSize : public Vector
{
    constexpr ScreenSize() = default;
    constexpr ScreenSize(int sx, int sy) : Vector(sx, sy) {}
};

/* Speed of objects in the world */
struct Speed : public Vector
{
    Speed() = default;
    constexpr Speed(int sx, int sy) : Vector(sx, sy) {}
};

/* Offset of a position. Positions cannot be added together, position and offset can. */
struct Offset : public Vector
{
    Offset() = default;
    constexpr Offset(int dx, int dy) : Vector(dx, dy) {}
    explicit Offset(Position pos) : Vector(pos.x, pos.y) {}

    [[nodiscard]] float GetSize() const { return float(std::sqrt(x * x + y * y)); }
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
constexpr bool operator!=(Position l, Position r) noexcept { return !operator==(l, r); }
constexpr bool operator==(Size l, Size r) noexcept { return l.x == r.x && l.y == r.y; }
constexpr ScreenPosition operator+(ScreenPosition v, Offset o) noexcept { return {v.x + o.x, v.y + o.y}; }
constexpr ScreenPosition operator+(ScreenPosition v, Size o) noexcept { return {v.x + o.x, v.y + o.y}; }
constexpr Offset operator-(ScreenPosition p, ScreenPosition o) noexcept { return {p.x - o.x, p.y - o.y}; }
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
constexpr bool operator==(Direction l, Direction r) noexcept { return l.dir == r.dir; }

/*
 *  Float math. Needed for shooting and raytracing of object trajectories.
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
constexpr PositionF operator+(PositionF v, SpeedF s) noexcept { return {v.x + s.x, v.y + s.y}; }
constexpr PositionF operator+(PositionF v, OffsetF o) noexcept { return {v.x + o.x, v.y + o.y}; }
constexpr PositionF & operator+=(PositionF & v, OffsetF o) noexcept
{
    v.x += o.x;
    v.y += o.y;
    return v;
}
constexpr bool operator==(PositionF l, PositionF r) { return l.x == r.x && l.y == r.y; }
constexpr bool operator!=(PositionF l, PositionF r) { return l.x != r.x || l.y != r.y; }
constexpr OffsetF operator*(DirectionF d, float m) noexcept { return {d.x * m, d.y * m}; }
constexpr OffsetF operator*(float m, DirectionF d) noexcept { return {m * d.x, m * d.y}; }

/* Base of all specific rectangles  */
template <typename PositionType>
struct RectBase
{
    PositionType pos;
    Size size;

    constexpr RectBase() = default;
    constexpr RectBase(PositionType pos, Size size) : pos(pos), size(size) {}
    constexpr RectBase(int pos_x, int pos_y, int size_x, int size_y) : pos{pos_x, pos_y}, size{size_x, size_y} {}

    constexpr int Left() const { return pos.x; }
    constexpr int Top() const { return pos.y; }
    constexpr int Right() const { return pos.x + size.x - 1; }
    constexpr int Bottom() const { return pos.y + size.y - 1; }
    constexpr bool IsInside(Vector vec) const
    {
        return vec.x >= this->Left() && vec.x <= this->Right() && vec.y >= this->Top() && vec.y <= this->Bottom();
    }
    [[nodiscard]] Vector MakeInside(Vector vec) const
    {
        return {std::clamp(vec.x, this->Left(), this->Right()), std::clamp(vec.y, this->Top(), this->Bottom())};
    }
    [[nodiscard]] PositionType Center() const { return {pos.x + size.x / 2, pos.y + size.y / 2}; }
    constexpr bool operator==(const RectBase & other)
    {
        return this->pos == other.pos && this->size == other.size;
    }
    constexpr bool operator!=(const RectBase & other) { return !this->operator==(other); }
};

/* Rectangle in units of our pixelated screen (render) surface */
struct ScreenRect : RectBase<ScreenPosition>
{
    constexpr ScreenRect() = default;
    constexpr ScreenRect(ScreenPosition pos, Size size) : RectBase{pos.x, pos.y, size.x, size.y} {}
    constexpr ScreenRect(int pos_x, int pos_y, int size_x, int size_y) : RectBase{pos_x, pos_y, size_x, size_y} {}
};

/* A rectangle in world/level coordinates.*/
struct Rect : RectBase<Position>
{
    constexpr Rect() = default;
    constexpr Rect(Position pos, Size size) : RectBase{pos.x, pos.y, size.x, size.y} {}
    constexpr Rect(int pos_x, int pos_y, int size_x, int size_y) : RectBase{pos_x, pos_y, size_x, size_y} {}
    explicit constexpr Rect(ScreenRect screen) : Rect{screen.pos.x, screen.pos.y, screen.size.x, screen.size.y} {}
    explicit operator ScreenRect() const {return ScreenRect{this->pos.x, this->pos.y, this->size.x, this->size.y};
}
};

/* Rectangle inside an image/bitmap used as a source of bliting to screen */
struct ImageRect : RectBase<Position>
{
    constexpr ImageRect() = default;
    constexpr ImageRect(Position pos, Size size) : RectBase{pos.x, pos.y, size.x, size.y} {}
    constexpr ImageRect(int pos_x, int pos_y, int size_x, int size_y) : RectBase{pos_x, pos_y, size_x, size_y} {}
};

/* Rectangle in native units of hosting window/surface. This needs to get used only if we want to draw an overlay over our pixelated surface */
struct NativeScreenRect : RectBase<NativeScreenPosition>
{
    constexpr NativeScreenRect() = default;
    constexpr NativeScreenRect(NativeScreenPosition pos, Size size) : RectBase{pos.x, pos.y, size.x, size.y} {}
    constexpr NativeScreenRect(int pos_x, int pos_y, int size_x, int size_y) : RectBase{pos_x, pos_y, size_x, size_y} {}
};

struct BoundingBox : RectBase<Position>
{
    BoundingBox() = default;
    BoundingBox(Size dimensions)
        : RectBase(-dimensions.x / 2, -dimensions.y / 2, dimensions.x, dimensions.y)
    {
        assert(dimensions.x % 2 && dimensions.y % 2);
    }
    bool IsInside(Position tested_position, Position entity_origin) const
    {
        return std::abs(tested_position.x - entity_origin.x) < this->size.x / 2 &&
               std::abs(tested_position.y - entity_origin.y) < this->size.y / 2;

        //return tested_position.x >= this->Left() + entity_origin.x &&
        //       tested_position.x <= this->Right() + entity_origin.x &&
        //       tested_position.y >= this->Top() + entity_origin.y &&
        //       tested_position.y <= this->Bottom() + entity_origin.y;
    }
};

//constexpr bool operator==(const NativeRect & left, const NativeRect & right) { return static_cast<Rect>(left) == static_cast<Rect>(right); }

using TankColor = char;
using Health = int;

enum class Orientation
{
    Horizontal,
    Vertical,
};

enum class HorizontalAlign
{
    Left,
    Right,
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
