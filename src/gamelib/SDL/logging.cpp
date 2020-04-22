#include <cstdio>
#include <cstdarg>
#include "require_sdl.h"
#ifdef _WIN32
#include <debugapi.h>
#endif

void     gamelib_print (const char *str, ...) {
	va_list l;
	va_start(l, str);
	vfprintf(stdout, str, l) ;
	va_end(l);
}

void     gamelib_debug (const char *str, ...) {
	va_list l;
	va_start(l, str);
	#ifdef _WIN32
	OutputDebugString(str);
	#else
	vfprintf(stderr, str, l) ;
	#endif
	va_end(l);
}

void     gamelib_error (const char *str, ...) {
	va_list l;
	va_start(l, str);
	vfprintf(stderr, str, l) ;
	va_end(l);
}

