#pragma once
#include "controllable.h"
#include "types.h"

struct LevelView
{
    constexpr static int Width = 159;
    constexpr static int Height = 99;

    const class Controllable * movable;
    const class Terrain * lvl;

  public:
    enum class QueryResult
    {
        Open,
        Collide,
        OutOfBounds,
    };

  public:
    LevelView(const Controllable * movable, const Terrain * lvl) : movable(movable), lvl(lvl) {}

    /* Some quick queries for use in AIs: */
    QueryResult QueryPoint(Offset offset) const;
    QueryResult QueryCircle(Offset offset) const;
};

//#define LSP_LOOKUP(lsp,x,y) ((lsp).data[(y)*LS_WIDTH+(x)])
