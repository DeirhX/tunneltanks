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
	constexpr Vector(int x, int y) : x(x), y(y) { }
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

	explicit Offset(Position pos): Vector(pos.x, pos.y) {}
};

/*
 *   Vector +- Vector -> Vector
 *   Position - Position -> Offset
 *   Position + Offset -> Position
 *   Speed * scalar -> Offset
 *   Size * scalar -> Size
 */

inline Vector operator+(Vector v, Vector o) { return { v.x + o.x, v.y + o.y }; }
inline Vector operator-(Vector v, Vector o) { return { v.x - o.x, v.y - o.y }; }
inline Offset operator*(Speed s, int t) { return { s.x * t, s.y * t }; }
inline Offset operator*(int t, Speed s) { return { s.x * t, s.y * t }; }
inline Size operator*(Size s, int t) { return { s.x * t, s.y * t }; }
inline Size operator*(int t, Size s) { return { s.x * t, s.y * t }; }
inline Size operator/(Size s, int t) { return { s.x / t, s.y / t }; }
inline Offset operator-(Position p, Position o) { return { p.x - o.x, p.y - o.y }; }
inline Position operator+(Position v, Offset o) { return { v.x + o.x, v.y + o.y }; }
inline Position operator+(Position v, Size o) { return { v.x + o.x, v.y + o.y }; }


/* Integer direction. Take your numerical keyboard and subtract 1 */
struct Direction
{
	int dir;
	[[nodiscard]] int Get() const { return this->dir; }
	int& Get() { return this->dir; }
	void Set(int new_dir) { this->dir = new_dir; }
	[[nodiscard]] Speed ToSpeed() const
	{
		return Speed{ static_cast<int>(this->dir) % 3 - 1, static_cast<int>(this->dir) / 3 - 1 };
	}
	static Direction FromSpeed(Speed speed)
	{
		return Direction{ speed.x + 1 + 3*(speed.y + 1) };
	}
	operator int() const { return dir; }
	
};
/* Oh no, a float! Can't we do without? */
/* TODO: we need actually just radians. But so often will we use the components it won't hurt to store them instead */
struct DirectionF
{
	float x;
	float y;
public:
	DirectionF() = default;
	DirectionF(float x, float y) : x(x), y(y) {}
	DirectionF(Direction int_direction)
	{
		auto speed = int_direction.ToSpeed();
		this->x = float(speed.x);
		this->y = float(speed.y);
	};
	
	[[nodiscard]] float GetSize() const { return std::sqrt(x * x + y * y); }
	[[nodiscard]] int ToIntDirection() const { return Direction::FromSpeed({ int(x), int(y) }); }
	[[nodiscard]] Speed ToSpeed() const
	{
		return Speed{ int(x), int(y) };
	}
	[[nodiscard]] DirectionF Normalize() const
	{
		auto size = GetSize();
		assert(size);
		return size ? DirectionF{ x / size, y / size } : DirectionF{ };
	}
};

/* Rectangle inside game world */
struct Rect {
	Position pos;
	Size size;
	Rect() = default;
    Rect(Position pos, Size size) : pos(pos), size(size) { }
	//Rect(ScreenPosition pos, Size size) : pos(pos), size(size) { }
	Rect(int pos_x, int pos_y, int size_x, int size_y) : pos{ pos_x, pos_y }, size{ size_x, size_y } { }

	int Left() const { return pos.x; }
	int Top() const { return pos.y; }
	int Right() const { return pos.x + size.x; }
	int Bottom() const { return pos.y + size.y; }
};

/* Rectangle in native units of hosting window/surface */
struct NativeRect : Rect
{
	NativeRect() = default;
	NativeRect(NativeScreenPosition pos, Size size) : Rect{ pos.x, pos.y, size.x, size.y } { }
	NativeRect(int pos_x, int pos_y, int size_x, int size_y) : Rect{ pos_x, pos_y, size_x, size_y } { }
};

struct Color
{
	unsigned char r{}, g{}, b{};
public:
	Color() = default;
    Color(unsigned char r, unsigned char g, unsigned char b) : r(r), g(g), b(b) { }
	Color Mask(Color other) const { return Color( (this->r * other.r) / 255, (this->g * other.g) / 255, (this->b * other.b) / 255 ); }
};

struct Color32 {
	unsigned char r{}, g{}, b{}, a{};
	Color32() = default;
	Color32(unsigned char r, unsigned char g, unsigned char b, unsigned char a) : r(r), g(g), b(b), a(a) { }
};

using TankColor = char;

enum class Orientation
{
	Horizontal,
	Vertical,
};


template<typename Type>
class holder_with_deleter : public std::unique_ptr<Type, void(*)(Type*)>
{
	using base = std::unique_ptr<Type, void(*)(Type*)>;
public:
	holder_with_deleter() : base{ nullptr, [](Type*) {} } {};
	holder_with_deleter(Type* value, void(*deleter)(Type*)) : base{ value, deleter } {}
};