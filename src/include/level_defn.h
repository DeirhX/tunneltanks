#ifndef _LEVEL_DEFINITION_H_
#define _LEVEL_DEFINITION_H_

#include <tweak.h>
#include <types.h>
#include <drawbuffer.h>

struct Level {
	char       *array;
	int    width;
	int    height;
	DrawBuffer *b;
	Vector      spawn[MAX_TANKS];
};


#endif /* _LEVEL_DEFINITION_H_ */
