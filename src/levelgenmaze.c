#include <cstdio>

#include <level.h>
#include <levelgenutil.h>
#include <memalloc.h>
#include <random.h>
#include <queue>

namespace levelgen::maze {

/* REMEMBER: The bases are centered in cells, so these cells need to larger: */
#define CELL_SIZE 60


/* MAZE-GENERATOR: */

/* This maze generator uses a Randomized DFS algorithm for maze generation. I'm
 * using this simply because it is one of the simplest algorithms to code, it's
 * reasonably quick, and the mazes look nice. :) */
 
typedef struct Cell {
	unsigned up    : 1; /* << Whether there is a wall in this direction. */
	unsigned right : 1;
	unsigned used  : 1; /* << Whether this cell is already used in a path. */
} Cell;

typedef struct Maze {
	Cell *data;
	int w, h;
} Maze;

typedef enum Dir {
	DIR_LEFT = 0,
	DIR_RIGHT,
	DIR_UP,
	DIR_DOWN,
	DIR_INVALID
} Dir;


static Dir pick_dir(Maze *m, int x, int y) {
	Dir      list[4];
	int i = 0;
	
	/* Which directions could we go from here? */
	if(x != 0      && !m->data[y*m->w + (x-1)].used) list[i++] = DIR_LEFT;
	if(x != m->w-1 && !m->data[y*m->w + (x+1)].used) list[i++] = DIR_RIGHT;
	if(y != 0      && !m->data[(y-1)*m->w + x].used) list[i++] = DIR_UP;
	if(y != m->h-1 && !m->data[(y+1)*m->w + x].used) list[i++] = DIR_DOWN;
	
	/* Pick one: */
	return (i==0) ? DIR_INVALID : list[Random.Int(0,i-1)];
}

/* Note: this doesn't do range checking, so make sure that dir is valid: */
static void remove_wall(Maze *m, Dir d, int x, int y) {
	if     (d==DIR_LEFT)  m->data[y*m->w + x-1].right = 0;
	else if(d==DIR_RIGHT) m->data[y*m->w + x].right = 0;
	else if(d==DIR_UP)    m->data[y*m->w + x].up = 0;
	else if(d==DIR_DOWN)  m->data[(y+1)*m->w + x].up = 0;
}

/* No range checking here, either: */
static void move_dir(Dir d, int *x, int *y) {
	if     (d==DIR_LEFT)  (*x)--;
	else if(d==DIR_RIGHT) (*x)++;
	else if(d==DIR_UP)    (*y)--;
	else if(d==DIR_DOWN)  (*y)++;
}

/* TODO: Use a vector to store the x/y values? */
static void maze_populate(Maze *m) {
	int i, x, y;
	auto queue = std::deque<Vector>(64);
	
	/* Quickly go through the maze, and add in all the walls: */
	for(i=0; i<m->w * m->h; i++) {
		m->data[i].up = m->data[i].right = 1;
		m->data[i].used = 0;
	}
	
	/* Now, pick a random cell: */
	x = Random.Int(0, m->w-1); y = Random.Int(0, m->h-1);
	
	do {
		Dir d = DIR_INVALID;
		
		/* Mark that cell as visited: */
		m->data[y*m->w+x].used = 1;
		
		/* Find somewhere to continue from, backtracking if needed: */
		while((d = pick_dir(m, x, y)) == DIR_INVALID && !queue.empty()) {
			Vector vv = queue.front();
			queue.pop_front();
			x = vv.x; y = vv.y;
		}
		
		/* If we were never able to get a direction, then we must be done: */
		if(d == DIR_INVALID) break;
		
		/* Else, let's move: */
		Vector v = Vector(x, y);
		queue.push_back(v);
		remove_wall(m, d, x, y);
		move_dir(d, &x, &y);
	} while(1);

}

/*
static void maze_dump(Maze *m) {
	int x, y;
	
	for(y=0; y<m->h; y++) {
		printf("|");
		for(x=0; x<m->w; x++) {
			Cell c = m->data[y*m->w+x];
			if     (c.up && c.right)  printf("⎴T");
			else if(c.up && !c.right) printf("⎴⎴");
			else if(!c.up && c.right) printf(" |");
			else                      printf("  ");
		}
		printf("\n");
	}
	
	for(x=0; x<m->w; x++) printf("⎴⎴");
	printf("⎴\n");
}
*/

static Maze *maze_new(Size size) {
	Maze *m = get_object(Maze);
	m->w = size.x; m->h = size.y;
	m->data = static_cast<Cell*>(get_mem(sizeof(Cell) * size.x * size.y));
	
	maze_populate(m);
	
	return m;
}

static void maze_free(Maze *m) {
	if(!m) return;
	free_mem(m->data);
	free_mem(m);
}


/* LEVEL-BUILDING LOGIC: */

static void invert_all(Level *lvl)
{
	lvl->ForEachVoxel([](LevelVoxel& voxel)
	{
		voxel = (voxel == LevelVoxel::LevelGenRock) ?
			LevelVoxel::LevelGenDirt :
			LevelVoxel::LevelGenRock;
	});
}


void maze_generator(Level *lvl) {
	int x, y;
	Maze *m = maze_new(lvl->GetSize()/CELL_SIZE);
	
	/* Reset all of the 'used' flags back to zero: */
	for(int i=0; i<m->w * m->h; i++)
		m->data[i].used = 0;
	
	/* Draw the maze: */
	for(y=0; y<m->h; y++) {
		for(x=0; x<m->w; x++) {
			Cell c = m->data[y*m->w+x];
			if(c.up)
				draw_line(lvl,
					Vector(x*CELL_SIZE,     y*CELL_SIZE), 
					Vector((x+1)*CELL_SIZE, y*CELL_SIZE), 0, 1);
			
			if(c.right)
				draw_line(lvl,
					Vector((x+1)*CELL_SIZE, y*CELL_SIZE), 
					Vector((x+1)*CELL_SIZE, (y+1)*CELL_SIZE), 0, 1);
		}
	}
	
	/* Draw a line up the left, so you can see the texture there too: */
	draw_line(lvl, Vector(0,0), Vector(0,m->h*CELL_SIZE), 0, 1);
	
	/* Fill in the unused space left behind on the right/bottom: */
	/* TODO: Have a fill_box() in levelgenutil.c? */
	for(y=0; y<lvl->GetSize().y; y++)
		for(x=m->w*CELL_SIZE; x<lvl->GetSize().x; x++)
			lvl->SetVoxel({ x, y }, LevelVoxel::LevelGenDirt);
	
	for(y=m->h*CELL_SIZE; y<lvl->GetSize().y; y++)
		for(x=0; x<m->w*CELL_SIZE; x++)
			lvl->SetVoxel({ x, y }, LevelVoxel::LevelGenDirt);
	
	/* Rough it up a little, and invert: */
	rough_up(lvl);
	invert_all(lvl);
	
	/* Add in the bases: */
	for(TankColor i=0; i<MAX_TANKS; i++) {
		int tx, ty;
		
		/* Pick a random spot for the base: */
		do {
			x = Random.Int(0, m->w-1); y = Random.Int(0, m->h-1);
		} while(m->data[y*m->w+x].used);
		
		/* Mark our spot, and the ones around it as well: */
		for(tx=-1; tx<=1; tx++)
			for(ty=-1; ty<=1; ty++)
				if(x+tx < m->w && y+ty < m->h)
					m->data[(y+ty)*m->w+(x+tx)].used = 1;
		
		/* Now, add it to the level: */
		lvl->SetSpawn(i, Position{
			x * CELL_SIZE + CELL_SIZE / 2,
			y * CELL_SIZE + CELL_SIZE / 2
			});
	}
	
	maze_free(m);
}

}
