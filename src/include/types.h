#ifndef _TYPES_H_
#define _TYPES_H_

/* Generic types that are used all over the place: */

/* A very simple struct used to store spawn locations of tanks: */
struct Vector {
	int x = 0, y = 0;
	Vector() {}
	Vector(int x, int y) : x(x), y(y) { }
};

using Position = Vector;
using Size = Vector;
using Speed = Vector;

/* A simple struct for quads: */
struct Rect {
	Position pos;
	Size size;
	Rect() { }
	Rect(Position pos, Size size) : pos(pos), size(size) { }
	Rect(int pos_x, int pos_y, int size_x, int size_y) : pos{ pos_x, pos_y }, size{ size_x, size_y } { }
};

/* A simple way to reference a color: */
typedef struct Color {
	unsigned char r, g, b;
} Color;
#define COLOR(r,g,b) {(r),(g),(b)}

#endif /* _TYPES_H_ */

