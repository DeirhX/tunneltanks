#pragma once
#include <vector>


#include "containers.h"
#include "types.h"

enum class LinkPointType
{
    Base,
    Machine,
};

struct NeighborLinkPoint
{
    class LinkPoint * point;
    float distance;
};

class LinkPoint
{
    LinkPointType type;
    Position position;

    class LinkMap * owner;

    std::vector<NeighborLinkPoint> possible_links;
    bool is_alive = true;

    bool is_connected = false;
  public:
    LinkPoint(Position position, LinkPointType type_, LinkMap * owner_ = nullptr)
        : type(type_), position(position), owner(owner_) {}
    ~LinkPoint() { Invalidate(); }

    [[nodiscard]] bool IsInvalid() const { return !is_alive; }
    void Invalidate();

    [[nodiscard]] LinkPointType GetType() const { return this->type; }
    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] bool IsConnected() const { return this->is_connected; }
    [[nodiscard]] const std::vector<NeighborLinkPoint> & GetNeighbors() const { return this->possible_links; }
    [[nodiscard]] NeighborLinkPoint GetClosestUnconnectedPoint() const;

    void SetPosition(Position position_);
    void SetConnected(bool connected) { this->is_connected = connected; }
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
    Link(LinkPoint * from_, LinkPoint * to_) : from(from_), to(to_)
    {
        assert(from_ && to_);
        this->from->SetConnected(true);
        this->to->SetConnected(true);
    }
    ~Link() { Invalidate(); }
    bool IsInvalid() const { return !is_alive; }
    void Invalidate() { is_alive = false; }
};

class LinkMap
{
    Level * level;
    ValueContainer<LinkPoint> link_points = {};
    std::vector<Link> links = {};
    bool modified = false;

  public:
    LinkMap(Level * level) : level(level) {}

    [[nodiscard]] ValueContainerView<LinkPoint> & GetLinkPoints() { return this->link_points; }

    template <class... LinkPointArgs>
    LinkPoint * RegisterLinkPoint(LinkPointArgs &&... args)
    {
        modified = true;
        return &this->link_points.ConstructElement(std::forward<LinkPointArgs>(args)..., this);
    }
    void UnregisterPoint(LinkPoint * point);
    void UpdateLinksToPoint(LinkPoint * point);

    void SolveLinks();

    void Advance();
};

