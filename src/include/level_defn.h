#pragma once
#include <tweak.h>
#include <types.h>
#include <drawbuffer.h>

struct Level
{
	char   *array;
	int    width;
	int    height;
	DrawBuffer *b;
	Vector      spawn[MAX_TANKS];

public:
	char GetAt(Position pos);
};



