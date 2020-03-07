#include <cstdio>
#include <cstdarg>
#include "require_sdl.h"

void     gamelib_print (const char *str, ...) {
	va_list l;
	va_start(l, str);
	vfprintf(stdout, str, l) ;
	va_end(l);
}

void     gamelib_debug (const char *str, ...) {
	va_list l;
	va_start(l, str);
	vfprintf(stderr, str, l) ;
	va_end(l);
}

void     gamelib_error (const char *str, ...) {
	va_list l;
	va_start(l, str);
	vfprintf(stderr, str, l) ;
	va_end(l);
}

