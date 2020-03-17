#pragma once

/* Generic types that are used all over the place. 
   Conversions possible only when it is conceptually sensible - enforce clear semantics
*/
struct Vector
{
	int x = 0, y = 0;
	Vector() = default;
	constexpr Vector(int x, int y) : x(x), y(y) { }
};

struct ScreenPosition : public Vector
{
	ScreenPosition() = default;
	ScreenPosition(int x, int y) : Vector(x, y) {}

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


/* Rectangle */
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

enum class Direction
{
	Horizontal,
	Vertical,
};
