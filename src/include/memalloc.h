#ifndef _MEM_ALLOC_H_
#define _MEM_ALLOC_H_

/* #define macros for using this stuff: */
#define get_mem(ammount) __get_mem((ammount), __FILE__, __LINE__)
#define get_object(type) (type *) get_mem( sizeof(type) )

void *__get_mem(int ammount, const char *file, int line) ;
void free_mem(void *ptr) ;
void print_mem_stats() ;

#endif /* _MEM_ALLOC_H_ */

