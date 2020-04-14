#pragma once
#include <vector>


#include "containers.h"
#include "types.h"

class LinkPoint
{
    Position position;

    bool is_alive = true;

  public:
    LinkPoint(Position position) : position(position) {}
    ~LinkPoint() { Invalidate(); }
    bool IsInvalid() const { return !is_alive; }
    void Invalidate() { is_alive = false; }
};

class Link
{
    LinkPoint * from = {};
    LinkPoint * to = {};

    bool is_alive = true;
  public:
    ~Link() { Invalidate(); }
    bool IsInvalid() const { return !is_alive; }
    void Invalidate() { is_alive = false; }
};

class LinkMap
{
    Level * level;
    ValueContainer<LinkPoint> link_points = {};
    ValueContainer<Link> links = {};
public:
    LinkMap(Level * level) : level(level) {}

    LinkPoint * RegisterLinkPoint(LinkPoint && temp_point);

    void UpdateAll();
};