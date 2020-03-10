#include <cstdio>

#include <level.h>
#include <levelgenutil.h>
#include <memalloc.h>
#include <queue>
#include <random.h>

namespace levelgen::braid
{

/* REMEMBER: The bases are centered in cells, so these cells need to larger: */
#define CELL_SIZE 60


/* BRAID-GENERATOR: */

/* This generator will randomly shuffle all possible edges, and then add them
 * so long as it doesn't leave some part of the maze unreachable, or create a
 * dead-end path. This is in no way optimal, but I do like the maps that it
 * creates... */

typedef struct Cell {
	unsigned up    : 1; /* << Whether there is a wall in this direction. */
	unsigned right : 1;
	unsigned flag  : 1; /* << Whether this cell is already used in a path. */
} Cell;

typedef struct Braid {
	Cell *data;
	Size size;
} Braid;

typedef enum Dir {
	DIR_LEFT = 0,
	DIR_RIGHT,
	DIR_UP,
	DIR_DOWN,
	DIR_INVALID
} Dir;

typedef struct Wall {
	int x, y;
	Dir      d;
} Wall;


/* This is a recursive function used by braid_is_connected: */
static void flood_fill(Braid *b, int x, int y) {
	
	/* Skip if we've already been filled: */
	if(b->data[y*b->size.x + x].flag) return;
	
	/* Color ourselves: */
	b->data[y*b->size.x + x].flag = 1;
	
	/* Try to recurse into our neighbors: */
	if(x > 0      && !b->data[y*b->size.x + (x-1)].right) flood_fill(b, x-1, y);
	if(x < b->size.x-1 && !b->data[y*b->size.x + x].right)     flood_fill(b, x+1, y);
	if(y > 0      && !b->data[y*b->size.x + x].up)        flood_fill(b, x,   y-1);
	if(y < b->size.y-1 && !b->data[(y+1)*b->size.x + x].up)    flood_fill(b, x,   y+1);
}

/* This will check a map for a cut-off region: */
static int braid_is_connected(Braid *b) {
	int i;
	
	/* Set all flags to 0: */
	for(i=0; i<b->size.x * b->size.y; i++)
		b->data[i].flag = 0;
	
	/* Recursively color all of the nodes: */
	flood_fill(b, 0, 0);
	
	/* Check for an uncolored node: */
	for(i=0; i<b->size.x * b->size.y; i++)
		if(!b->data[i].flag) return 0;
	
	return 1;
}

/* This will check to see if adding a wall will make a node a dead end: */
static int braid_node_dead_end(Braid *b, int x, int y, Dir d) {
	int up, down, left, right;
	
	/* Check for all of the walls: */
	up    = (d==DIR_UP)    ? 1 : (y==0)      ? 1 : b->data[y*b->size.x+x].up;
	down  = (d==DIR_DOWN)  ? 1 : (y==b->size.y-1) ? 1 : b->data[(y+1)*b->size.x+x].up;
	left  = (d==DIR_LEFT)  ? 1 : (x==0)      ? 1 : b->data[y*b->size.x+x-1].right;
	right = (d==DIR_RIGHT) ? 1 : (x==b->size.x-1) ? 1 : b->data[y*b->size.x+x].right;
	
	/* If this node was made a dead end, we'll have 3 walls: */
	return up + down + left + right == 3;
}

/* This will COMPLETELY check to see if a new wall makes a dead end: */
static int braid_makes_dead_end(Braid *b, int x, int y, Dir d) {
	/* See if it causes a dead end on this node: */
	if(braid_node_dead_end(b,x,y,d)) return 1;
	
	/* Now check to see if it dead ends on the other side of the wall: */
	if (d == DIR_UP    && y > 0)
		return braid_node_dead_end(b, x, y-1, DIR_DOWN);
	
	else if(d == DIR_DOWN  && y < b->size.y-1)
		return braid_node_dead_end(b, x, y+1, DIR_UP);
	
	else if(d == DIR_LEFT  && x > 0)
		return braid_node_dead_end(b, x-1, y, DIR_RIGHT);
	
	else if(d == DIR_RIGHT && x < b->size.x-1)
		return braid_node_dead_end(b, x+1, y, DIR_LEFT);
	
	return 0;
}

/* This will shuffle a list of walls: */
static void wall_shuffle(Wall *w, int len) {
	int i;
	
	/* Run through the list once, shuffling everything: */
	for(i=0; i<len; i++) {
		int new_index = Random.Int(0,len-1);
		Wall temp = w[i];
		w[i] = w[new_index];
		w[new_index] = temp;
	}	
}

/* Fills in a Braid object with a braid maze: */
static void braid_populate(Braid *b) {
	Wall *w;
	int i;
	int x, y;
	
	/* Set all walls to empty: */
	for(i=0; i<b->size.x * b->size.y; i++)
		b->data[i].up = b->data[i].right = 0;
	
	/* Get, and fill our list of walls: */
	w = static_cast<Wall*>(get_mem(sizeof(Wall) * b->size.x * b->size.y * 2));
	x = y = 0;
	for(i=0; i < b->size.x * b->size.y * 2; i+=2) {
		w[i]   = Wall{x, y, DIR_UP};
		w[i+1] = Wall{x, y, DIR_RIGHT};
		
		if(++x==b->size.x) {
			y++;
			x=0;
		}
	}
	
	/* Shuffle those walls: */
	wall_shuffle(w, b->size.x * b->size.y * 2);
	
	/* Now, go through each wall, and add it if it blocks something off, or
	 * makes a dead-end: */
	for(i=0; i<b->size.x * b->size.y * 2; i++) {
		
		/* Check that this edge isn't on a border: */
		if((y==0&&w[i].d==DIR_UP) || (x==b->size.x-1&&w[i].d==DIR_RIGHT)) continue;
		
		/* Check that this edge won't make a dead end bit: */
		if(braid_makes_dead_end(b, w[i].x, w[i].y, w[i].d)) continue;
		
		/* Set this wall: */
		if     (w[i].d == DIR_UP)    b->data[w[i].y*b->size.x+w[i].x].up = 1;
		else if(w[i].d == DIR_RIGHT) b->data[w[i].y*b->size.x+w[i].x].right = 1;
		
		/* Now check for isolated regions: */
		if(!braid_is_connected(b)) {
			/* Oops. Blocked something off. Undo that... */
			if     (w[i].d == DIR_UP)    b->data[w[i].y*b->size.x+w[i].x].up = 0;
			else if(w[i].d == DIR_RIGHT) b->data[w[i].y*b->size.x+w[i].x].right = 0;
		}
	}
	
	/* Done? */
	free_mem(w);
}

static Braid *braid_new(Size size) {
	Braid *b = get_object(Braid);
	b->size = size;
	b->data = static_cast<Cell*>(get_mem(sizeof(Cell) * size.x * size.y));
	
	braid_populate(b);
	
	return b;
}

static void braid_free(Braid *b) {
	if(!b) return;
	free_mem(b->data);
	free_mem(b);
}


/* LEVEL-BUILDING LOGIC: */

static void invert_all(Level *lvl)
{
	lvl->ForEachVoxel([](LevelVoxel& voxel) { voxel = !voxel; });
}


void braid_generator(Level *lvl) {
	int i, x, y;
	Braid *b = braid_new(lvl->GetSize() / CELL_SIZE);
	
	/* Reset all of the 'used' flags back to zero: */
	for(i=0; i<b->size.x * b->size.y; i++)
		b->data[i].flag = 0;
	
	/* Draw the maze: */
	for(y=0; y<b->size.y; y++) {
		for(x=0; x<b->size.x; x++) {
			Cell c = b->data[y*b->size.x+x];
			if(c.up)
				draw_line(lvl,
					Vector(x*CELL_SIZE,     y*CELL_SIZE), 
					Vector((x+1)*CELL_SIZE, y*CELL_SIZE), 0, 1);
			
			if(c.right)
				draw_line(lvl,
					Vector((x+1)*CELL_SIZE, y*CELL_SIZE), 
					Vector((x+1)*CELL_SIZE, (y+1)*CELL_SIZE), 0, 1);
			
			if(!c.up && !c.right)
				set_circle(lvl, (x+1)*CELL_SIZE, y*CELL_SIZE, 0);
		}
	}
	
	/* Draw a line up the left, so you can see the texture there too: */
	draw_line(lvl, Vector(0,0), Vector(0,b->size.y*CELL_SIZE), 0, 1);
	
	/* Fill in the unused space left behind on the right/bottom: */
	/* TODO: Have a fill_box() in levelgenutil.c? */
	for(y=0; y<lvl->GetSize().y; y++)
		for(x=b->size.x*CELL_SIZE; x<lvl->GetSize().x; x++)
			lvl->SetVoxelRaw({x, y}, 0);
	
	for(y=b->size.y*CELL_SIZE; y<lvl->GetSize().y; y++)
		for(x=0; x<b->size.x*CELL_SIZE; x++)
			lvl->SetVoxelRaw({x, y}, 0);
	
	/* Rough it up a little, and invert: */
	rough_up(lvl);
	invert_all(lvl);
	
	/* Add in the bases: */
	for(i=0; i<MAX_TANKS; i++) {
		int tx, ty;
		
		/* Pick a random spot for the base: */
		do {
			x = Random.Int(0, b->size.x-1); y = Random.Int(0, b->size.y-1);
		} while(b->data[y*b->size.x+x].flag);
		
		/* Mark our spot, and the ones around it as well: */
		for(tx=-1; tx<=1; tx++)
			for(ty=-1; ty<=1; ty++)
				if(x+tx < b->size.x && y+ty < b->size.y)
					b->data[(y+ty)*b->size.x+(x+tx)].flag = 1;
		
		/* Now, add it to the level: */
		lvl->SetSpawn(i, Position{
			x * CELL_SIZE + CELL_SIZE / 2,
			y * CELL_SIZE + CELL_SIZE / 2
			});
	}
	
	braid_free(b);
}

}