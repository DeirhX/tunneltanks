#pragma once
#include "types.h"


struct LevelView
{
	constexpr static int Width = 159;
	constexpr static int Height = 99;

	struct Tank* tank;
	struct Level* lvl;
public:
	enum class QueryResult {
		Open,
		Collide,
		OutOfBounds,
	};

public:
	LevelView(Tank* tank, Level* lvl) : tank(tank), lvl(lvl) {}

	/* Some quick queries for use in AIs: */
	QueryResult QueryPoint(Offset offset);
	QueryResult QueryCircle( Offset offset);
};

//#define LSP_LOOKUP(lsp,x,y) ((lsp).data[(y)*LS_WIDTH+(x)])


