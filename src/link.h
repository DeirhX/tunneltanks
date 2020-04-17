#pragma once
#include <vector>


#include "containers.h"
#include "types.h"

class LinkPoint
{
    Position position;

    class LinkMap * owner;

    std::vector<LinkPoint *> possible_links;
    bool is_alive = true;

  public:
    LinkPoint(Position position, LinkMap * owner_ = nullptr) : position(position), owner(owner_) {}
    ~LinkPoint() { Invalidate(); }
    bool IsInvalid() const { return !is_alive; }
    void Invalidate();

    [[nodiscard]] Position GetPosition() const { return this->position; }

    void SetPosition(Position position_);
    void RemovePossibleLink(LinkPoint * possible_link);
    void UpdateLink(LinkPoint * possible_link); 
    void UpdateAllLinks();
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

    template <class... LinkPointArgs>
    LinkPoint * RegisterLinkPoint(LinkPointArgs &&... args)
    {
        return &this->link_points.ConstructElement(std::forward<LinkPointArgs>(args)..., this);
    }
    //LinkPoint * RegisterLinkPoint(LinkPoint && temp_point);
    void UnregisterPoint(LinkPoint * point);

    void UpdateAll();

    ValueContainerView<LinkPoint> & GetLinkPoints() { return this->link_points; }
};

