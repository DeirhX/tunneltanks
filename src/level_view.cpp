#include "level_view.h"
#include "level.h"
#include "tank.h"
#include <cstdlib>

LevelView::QueryResult LevelView::QueryPoint(Offset offset)
{
	Position pos = tank->GetPosition();

	if (abs(offset.x) >= Width / 2 || abs(offset.y) >= Height / 2) return QueryResult::OutOfBounds;

	LevelPixel c = this->lvl->GetPixel(pos + offset);
	if (Pixel::IsBlockingCollision(c)) return QueryResult::Collide;
	return QueryResult::Open;
}

LevelView::QueryResult LevelView::QueryCircle(Offset offset)
{
	for (int dy = offset.y - 3; dy <= offset.y + 3; dy++)
		for (int dx = offset.x - 3; dx <= offset.x + 3; dx++) {
			/* Don't take out the corners: */
			if ((dx == offset.x - 3 || dx == offset.x + 3) && (dy == offset.y - 3 || dy == offset.y + 3)) continue;

			auto result = QueryPoint(Offset{ dx, dy });
			if (result == QueryResult::OutOfBounds || result == QueryResult::Collide) return result;
		}

	return QueryResult::Open;
}


//
//void level_slice_copy(LevelView ls, LevelSliceCopy *lsc) {
//	Position pos = ls.t->GetPosition();
//	
//	for(int y = -LS_HEIGHT / 2; y<=LS_HEIGHT/2; y++)
//		for(int x = -LS_WIDTH / 2; x<=LS_WIDTH/2; x++)
//			lsc->data[y*LS_WIDTH+x] = level_get(ls.lvl, pos.x+x, pos.y+y);
//}
//
//