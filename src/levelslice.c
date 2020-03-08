#include <cstdlib>

#include <level.h>
#include <levelslice.h>
#include <memalloc.h>
#include <tweak.h>
#include <tank.h>


LevelSliceQuery level_slice_query_point(LevelSlice ls, int x, int y) {
	char c;

	Position pos = ls.t->GetPosition();
	
	if(abs(x) >= LS_WIDTH/2 || abs(y) >= LS_HEIGHT/2) return LSQ_OUT_OF_BOUNDS;
	c = level_get(ls.lvl, pos.x+x, pos.y+y);

	if(c==DIRT_HI || c==DIRT_LO || c==BLANK) return LSQ_OPEN;
	return LSQ_COLLIDE;
}


LevelSliceQuery level_slice_query_circle(LevelSlice ls, int x, int y) {
	int dx, dy;
	
	for(dy=y-3; dy<=y+3; dy++)
		for(dx=x-3; dx<=x+3; dx++) {
			LevelSliceQuery out;
			
			/* Don't take out the corners: */
			if((dx==x-3 || dx==x+3) && (dy==y-3 || dy==y+3)) continue;
			
			out = level_slice_query_point(ls, dx, dy);
			if(out==LSQ_OUT_OF_BOUNDS || out==LSQ_COLLIDE) return out;
		}
	
	return LSQ_OPEN;
}


void level_slice_copy(LevelSlice ls, LevelSliceCopy *lsc) {
	
	int x, y;
	
	Position pos = ls.t->GetPosition();
	
	for(y=-LS_HEIGHT/2; y<=LS_HEIGHT/2; y++)
		for(x=-LS_WIDTH/2; x<=LS_WIDTH/2; x++)
			lsc->data[y*LS_WIDTH+x] = level_get(ls.lvl, pos.x+x, pos.y+y);
}

