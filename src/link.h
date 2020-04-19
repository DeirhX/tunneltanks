#pragma once
#include <optional>
#include <vector>


#include "containers.h"
#include "duration.h"
#include "tweak.h"
#include "types.h"

class Surface;

enum class LinkPointType
{
    Base,
    Machine, /* Placed machine */
    Transit, /* Not yet placed */
    Tank,    /* Tank being charged */
};

struct NeighborLinkPoint
{
    class LinkPoint * point;
    float distance;
    //class Link * active_link = nullptr;
};

/* LinkPoint: Linkable point to others */
class LinkPoint
{
    LinkPointType type;
    Position position;

    class LinkMap * owner;

    std::vector<NeighborLinkPoint> possible_links;
    int connected_links = 0;

    bool is_alive = true; /* Once false, object is dead forever */
    bool is_enabled = true;
    bool is_part_of_graph = false;
  public:
    LinkPoint(Position position, LinkPointType type_, LinkMap * owner_ = nullptr);
    //LinkPoint(LinkPoint && movable) noexcept;
    ~LinkPoint() { Invalidate(); }

    [[nodiscard]] bool IsInvalid() const { return !is_alive; }
    void Invalidate();

    [[nodiscard]] LinkPointType GetType() const { return this->type; }
    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] LinkMap * GetLinkMap() const { return this->owner; }
    [[nodiscard]] bool IsOrphaned() const { return !this->is_part_of_graph; } /* Used by link solver.*/
    [[nodiscard]] const std::vector<NeighborLinkPoint> & GetNeighbors() const { return this->possible_links; }
    [[nodiscard]] std::optional<NeighborLinkPoint> GetClosestOrphanedPoint() const;
    [[nodiscard]] bool IsInRange(LinkPoint * other_link) const;
    [[nodiscard]] bool IsEnabled() const { return this->is_enabled; }

    void SetPosition(Position position_);

    void SetIsPartOfGraph(bool value) { this->is_part_of_graph = value; }
    //void ConnectWith(class Link * link);
    //void DisconnectWith(class Link * link);
    //void DisconnectAll();

    void RemovePossibleLink(LinkPoint * possible_link);
    void UpdateLink(LinkPoint * possible_link); 
    void ComputePossibleLinks();

    void Disable() { this->is_enabled = false; }
    void Enable() { this->is_enabled = true; }
};

/* LinkPointSource: Wrapper to manage the lifetime of LinkPoint that resides in LinkMap array.
 */
class LinkPointSource
{
private:
    LinkPoint * link_point = nullptr;

public:
    LinkPointSource() = default;
    LinkPointSource(class World * world, Position position, LinkPointType type);
    /* Ban copies, force move to transfer ownership of link_point */
    LinkPointSource(const LinkPointSource &) = delete;
    LinkPointSource & operator=(const LinkPointSource &) = delete;
    LinkPointSource(LinkPointSource && movable) noexcept { *this = std::move(movable); }
    LinkPointSource & operator=(LinkPointSource && movable) noexcept;
    ~LinkPointSource() { Destroy(); }

    void Detach() { link_point = nullptr; }
    void Destroy();

    void UpdatePosition(Position position) { link_point->SetPosition(position); }
    void Disable() { this->link_point->Disable(); }
    void Enable() { this->link_point->Enable(); }
};


enum class LinkType
{
    Live,      /* Flowing normally */
    Blocked,     /* Blocked by and obstacle */
    Theoretical, /* Too far from source */
};

/* Link: A connected link between two points */
class Link : public Invalidable
{
    LinkPoint * from = {};
    LinkPoint * to = {};

    LinkType type;
    bool is_alive = true;
  public:
    Link(LinkPoint * from_, LinkPoint * to_);
    ~Link();
    Link(Link && movable) = default;
    Link & operator=(Link && movable) noexcept = default;

    [[nodiscard]] LinkType GetType() const { return this->type; }
    [[nodiscard]] LinkPoint * GetSource() const { return this->from; }
    [[nodiscard]] LinkPoint * GetTarget() const { return this->to; }
    void Draw(Surface * surface) const;
};

/* LinkMap: Manages all link point updates and links */
class LinkMap
{
    Level * level;
    ValueContainer<LinkPoint> link_points = {};
    std::vector<Link> links = {};

    RepetitiveTimer relink_timer = {tweak::world::LinkReactorsInterval};
    bool is_collection_modified = false;
    bool is_linkpoint_moved = false;

  public:
    LinkMap(Level * level) : level(level) {}

    [[nodiscard]] ValueContainerView<LinkPoint> & GetLinkPoints() { return this->link_points; }

    template <class... LinkPointArgs>
    LinkPoint * RegisterLinkPoint(LinkPointArgs &&... args)
    {
        is_collection_modified = true;
        return &this->link_points.ConstructElement(std::forward<LinkPointArgs>(args)..., this);
    }
    void UnregisterPoint(LinkPoint * point);
    void RemoveAll();

    void UpdateLinksToPoint(LinkPoint * point);

    void SolveLinks();

    void Advance();
    void Draw(Surface * surface) const;
};

