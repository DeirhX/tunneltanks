#pragma once
/* #define macros for using this stuff: */
#define get_mem(ammount) __get_mem((ammount), __FILE__, __LINE__)
#define get_object(type) (type *) get_mem( sizeof(type) )

void *__get_mem(int ammount, const char *file, int line) ;
void free_mem(void *ptr) ;
void print_mem_stats() ;



