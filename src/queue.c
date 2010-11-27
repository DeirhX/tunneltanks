#include <stdlib.h>
#include "queue.h"
#include "types.h"
#include "memalloc.h"

struct Queue {
	Vector *array;
	unsigned i, j, l, max;
} ;

static void resize(Queue *q) {
	register unsigned i, j;
	Vector *new;
	
	q->max *= 2;
	new     = get_mem(sizeof(Vector) * q->max);
	
	for(i=q->j, j=0; j<q->l; i++, j++) {
		if( i >= q->l ) i = 0;
		new[j] = q->array[i];
	}
	
	free_mem(q->array);
	q->array = new;
	q->i = 0; q->j = q->l;
}

Queue *queue_new(unsigned len) {
	Queue *q = get_object(Queue);
	q->array = get_mem(sizeof(Vector) * len);
	q->i = q->j = q->l = 0;
	q->max = len;
	return q;
}

void queue_destroy(Queue *q) {
	if(!q) return;
	free_mem(q->array);
	free_mem(q);
}

unsigned queue_length(Queue *q) {
	return q->l;
}

void queue_enqueue(Queue *q, Vector *v) {
	if(q->l == q->max) resize(q);
	q->array[q->j++] = *v;
	if(q->j >= q->max) q->j = 0;
	q->l++;
}

Vector queue_dequeue(Queue *q) {
	Vector out;
	if(q->l == 0) return (Vector){0,0};
	q->l--;
	out = q->array[q->i++];
	if(q->i >= q->max) q->i = 0;
	return out;
}

