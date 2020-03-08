#include <cstdlib>

#include <levelgenutil.h>
#include <level.h>
#include <types.h>
#include <random.h>

void fill_all(Level *lvl, LevelVoxel c) {
	lvl->ForEachVoxel([c](LevelVoxel& voxel) { voxel = c; });
}

void rough_up(Level *lvl) {
	int x, y;
	
	/* Sanitize our input: */
	lvl->ForEachVoxel([](LevelVoxel& voxel) { voxel = !!voxel; });
	
	/* Mark all spots that are blank, but next to spots that are marked: */
	for(x=0; x<lvl->GetSize().x; x++) {
		for(y=0; y<lvl->GetSize().y; y++) {
			int t = 0;
			
			if (lvl->GetVoxel({ x, y })) continue;
			
			t += (x!=0            )       && lvl->GetVoxel({ x - 1, y }) == 1;
			t += (x!=lvl->GetSize().x-1 ) && lvl->GetVoxel({ x + 1, y }) == 1;
			t += (y!=0            )       && lvl->GetVoxel({ x, y - 1 }) == 1;
			t += (y!=lvl->GetSize().y-1)  && lvl->GetVoxel({ x, y + 1 }) == 1;

			if(t) lvl->Voxel({ x, y }) = 2;
		}
	}
	
	/* For every marked spot, randomly fill it: */
	lvl->ForEachVoxel([](LevelVoxel& voxel)
	{
		if (voxel == 2)
			voxel = Random::Bool(500);
	});
}

Position pt_rand(Size size, int border) {
	Position out;
	out.x = Random::Int(border, size.x - border);
	out.y = Random::Int(border, size.y - border);
	return out;
}

/* Actually returns the distance^2, but points should still remain in the same
 * order, and this doesn't require a call to sqrt(): */
int pt_dist(Vector a, Vector b) {
	return (a.x-b.x)*(a.x-b.x) + (a.y-b.y)*(a.y-b.y);
}

/* Used for point drawing: */
static void set_point(Level *lvl, int x, int y, char value) {
	if(x >= lvl->GetSize().x || y >= lvl->GetSize().y) return;
	lvl->Voxel({ x, y }) = value;
}

void set_circle(Level *lvl, int x, int y, char value) {
	int tx, ty;
	for(ty=-3; ty<=3; ty++) {
		for(tx=-3; tx<=3; tx++) {
			if((tx==-3 || tx==3) && (ty==-3 || ty==3)) continue;
			set_point(lvl, x+tx, y+ty, value);
		}
	}
}

#define SWAP(type,a,b) do { type t = (a); (a)=(b); (b)=t; } while(0)

/* New Bresenham's Algorithm-based function: */

void draw_line(Level *dest, Vector a, Vector b, char value, int fat_line) {
	int swap, dx, dy, error, stepy;
	int x, y;
	void (*pt_func)(Level *, int, int, char) ;
	
	/* How is this thing getting drawn? */
	pt_func = (fat_line) ? set_circle : set_point;
	
	/* Swap x and y values when the graph gets too steep to operate normally: */
	if((swap = abs(b.y - a.y) > abs(b.x - a.x))) {
		SWAP(int, a.x, a.y);
		SWAP(int, b.x, b.y);
	}

	/* Swap a and b so that a is to the left of b: */
	if(a.x > b.x) SWAP(Vector, a, b);

	/* A few calculations: */
	dx = b.x - a.x;
	dy = /*abs*/(b.y - a.y);
	error = dx / 2;
	stepy = (a.y < b.y) ? 1 : -1;
	
	/* Now, for every x from a.x to b.x, add the correct dot: */
	for (x = a.x, y=a.y; x <= b.x; x++) {
		if(swap) pt_func(dest, y, x, value);
		else     pt_func(dest, x, y, value);

		error -= dy;
		if(error < 0) {
			y     += stepy;
			error += dx;
		}
	}
}


/* Original DDA-based function:

#define ROUND(x) ((int)((x)+0.5))
#define SWAP(a,b) do { Vector t = (a); (a)=(b); (b)=t; } while(0)

static void draw_line(Level *dest, Vector a, Vector b) {
	double x, y, dx, dy, stepx, stepy;
	
	dx = (double)a.x - (double)b.x;
	dy = (double)a.y - (double)b.y;
	
	if(dx==0 && dy==0) {
		set_circle(dest->array, dest->width, a.x, a.y);
		return;
	
	} else if(dx==0 || abs(dy)>abs(dx)) {
		if(a.y > b.y) SWAP(a,b);
		stepx = dx / dy; stepy = 1;
	 
	} else {
		if(a.x > b.x) SWAP(a,b);
		stepx = 1; stepy = dy / dx;
	}
	
	x = a.x; y = a.y;
	while ((stepx==1 && x<=b.x) || (stepy==1 && y<=b.y)) {
		set_circle(dest->array, dest->width, ROUND(x), ROUND(y));
		x += stepx; y += stepy;
	}
}
*/

